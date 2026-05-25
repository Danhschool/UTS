using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Đọc vision RT của local human và cập nhật <see cref="IHideable"/> (không phải phe local).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FactionVisibilityUpdater : MonoBehaviour
    {
        const float VisionThreshold = 0.9f;

        [SerializeField] Camera visionFogCamera;
        [SerializeField] LocalHumanOwnerService localHumanOwnerService;
        [SerializeField, Min(1)] private int visionReadIntervalFrames = 2;
        [SerializeField, Min(32)] private int hideablesUpdatedPerFrame = 250;
        [SerializeField, Min(15)] private int pruneHideablesIntervalFrames = 90;

        Camera boundVisionCamera;
        Owner localOwner = Owner.Player1;
        readonly FogCpuTextureMirror visionMirror = new();
        readonly List<IHideable> hideables = new(1024);
        readonly HashSet<IHideable> hideableLookup = new(1024);
        int hideableCursor;
        int lastPruneFrame = -1;
        int _deferredRebuildFramesRemaining = -1;
        const int DeferredRebuildFrameDelay = 5;

        void Awake()
        {
            if (localHumanOwnerService == null)
            {
                localHumanOwnerService = LocalHumanOwnerService.Instance;
            }

            if (visionFogCamera == null)
            {
                visionFogCamera = GetComponent<Camera>();
            }

            BindVisionCamera(visionFogCamera);
            ResolveLocalOwner();

            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
            Bus<PlaceholderSpawnEvent>.RegisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.RegisterForAll(HandlePlaceholderDestroy);
            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] += HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] += HandleSupplyDepleted;
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += HandleLocalOwnerChanged;
            ResolveLocalOwner();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= HandleLocalOwnerChanged;
        }

        void OnDestroy()
        {
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
            Bus<PlaceholderSpawnEvent>.UnregisterForAll(HandlePlaceholderSpawn);
            Bus<PlaceholderDestroyEvent>.UnregisterForAll(HandlePlaceholderDestroy);
            Bus<SupplySpawnEvent>.OnEvent[Owner.Unowned] -= HandleSupplySpawn;
            Bus<SupplyDepletedEvent>.OnEvent[Owner.Unowned] -= HandleSupplyDepleted;

            visionMirror.Release();
        }

        public void BindVisionCamera(Camera camera)
        {
            boundVisionCamera = camera != null ? camera : visionFogCamera;
            visionMirror.Release();
            hideableCursor = 0;
            _cachedLocalVisionSampleRoot = null;
        }

        /// <summary>
        /// Mục tiêu: Rig P1 tắt nhưng updater vẫn LateUpdate — tránh log P2-E với RT P1.
        /// Cách hoạt động: Chỉ chạy khi branch fog khớp localOwner và component enabled.
        /// </summary>
        bool IsActivePresentationBranch()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            FactionFogPresentation presentation = GetComponentInParent<FactionFogPresentation>(true);
            if (presentation == null)
            {
                return true;
            }

            return presentation.PresentationOwner == localOwner
                   && presentation.isActiveAndEnabled
                   && presentation.gameObject.activeInHierarchy;
        }

        public void BindLocalOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            bool ownerChanged = localOwner != owner;
            localOwner = owner;

            if (ownerChanged)
            {
                _cachedLocalVisionSampleRoot = null;
            }

            if (ownerChanged || hideables.Count == 0)
            {
                _deferredRebuildFramesRemaining = DeferredRebuildFrameDelay;
            }
        }

        /// <summary>
        /// Mục tiêu: Sau khi SyncOwnerAndFogVision — vision RT đã có blob layer 15, cập nhật lại hideables.
        /// Cách hoạt động: Read vision RT rồi full-pass ApplyVisibility (không quét lại toàn scene).
        /// </summary>
        public void RefreshVisibilityAfterVisionLayers()
        {
            RefreshAllHideableVisibility(forceFullPass: true);
        }

        void HandleLocalOwnerChanged(Owner owner) => BindLocalOwner(owner);

        public void RebuildHideablesForLocalOwner()
        {
            hideables.Clear();
            hideableLookup.Clear();
            hideableCursor = 0;

            AbstractCommandable[] commandables = FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable commandable = commandables[i];
                if (commandable == null)
                {
                    continue;
                }

                if (ShouldTrackAsHideable(commandable.Owner))
                {
                    TrackHideable(commandable);
                }
                else
                {
                    commandable.SetVisible(true);
                }
            }

            GatherableSupply[] supplies = FindObjectsByType<GatherableSupply>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < supplies.Length; i++)
            {
                if (supplies[i] != null)
                {
                    TrackHideable(supplies[i]);
                }
            }

            Placeholder[] placeholders = FindObjectsByType<Placeholder>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < placeholders.Length; i++)
            {
                Placeholder placeholder = placeholders[i];
                if (placeholder == null)
                {
                    continue;
                }

                if (ShouldTrackAsHideable(placeholder.Owner))
                {
                    TrackHideable(placeholder);
                }
                else
                {
                    placeholder.SetVisible(true);
                }
            }

            if (TryIsVisionRtReadyForMassUpdate())
            {
                RefreshAllHideableVisibility(forceFullPass: true);
            }
            else
            {
                SchedulePostRebuildVisibilityRefresh();
                ScheduleVisionMassUpdateRetry();
            }
        }

        int _postRebuildRefreshFramesRemaining = -1;
        int _visionMassUpdateRetryAttemptsRemaining = -1;
        int _visionMassUpdateRetryNextCheckFrame = -1;
        const int VisionMassUpdateRetryMaxAttempts = 15;
        const int VisionMassUpdateRetryIntervalFrames = 6;
        const float VisionReadyThreshold = 0.1f;
        Transform _cachedLocalVisionSampleRoot;

        void SchedulePostRebuildVisibilityRefresh() =>
            _postRebuildRefreshFramesRemaining = 2;

        void ScheduleVisionMassUpdateRetry()
        {
            _visionMassUpdateRetryAttemptsRemaining = VisionMassUpdateRetryMaxAttempts;
            _visionMassUpdateRetryNextCheckFrame = Time.frameCount + VisionMassUpdateRetryIntervalFrames;
        }

        void LateUpdate()
        {
            if (!IsActivePresentationBranch())
            {
                return;
            }

            if (_deferredRebuildFramesRemaining >= 0)
            {
                if (_deferredRebuildFramesRemaining == 0)
                {
                    RebuildHideablesForLocalOwner();
                    _deferredRebuildFramesRemaining = -1;
                }
                else
                {
                    _deferredRebuildFramesRemaining--;
                }
            }
            else if (_postRebuildRefreshFramesRemaining >= 0)
            {
                if (_postRebuildRefreshFramesRemaining == 0)
                {
                    if (TryIsVisionRtReadyForMassUpdate())
                    {
                        RefreshVisibilityAfterVisionLayers();
                    }
                    else
                    {
                        ScheduleVisionMassUpdateRetry();
                    }

                    _postRebuildRefreshFramesRemaining = -1;
                }
                else
                {
                    _postRebuildRefreshFramesRemaining--;
                }
            }
            else if (_visionMassUpdateRetryAttemptsRemaining > 0
                     && Time.frameCount >= _visionMassUpdateRetryNextCheckFrame)
            {
                _visionMassUpdateRetryNextCheckFrame =
                    Time.frameCount + VisionMassUpdateRetryIntervalFrames;
                _visionMassUpdateRetryAttemptsRemaining--;

                if (TryIsVisionRtReadyForMassUpdate())
                {
                    RefreshAllHideableVisibility(forceFullPass: true);
                    _visionMassUpdateRetryAttemptsRemaining = -1;
                }
            }

            if (boundVisionCamera == null
                || !boundVisionCamera.isActiveAndEnabled
                || boundVisionCamera.targetTexture == null)
            {
                return;
            }

            visionMirror.TryRefresh(boundVisionCamera.targetTexture, visionReadIntervalFrames);
            if (!visionMirror.HasCpuTexture)
            {
                return;
            }

            MaybePruneHideables();
            UpdateHideablesRoundRobin();
        }

        void RefreshAllHideableVisibility(bool forceFullPass)
        {
            if (boundVisionCamera == null || boundVisionCamera.targetTexture == null)
            {
                return;
            }

            if (forceFullPass && !TryIsVisionRtReadyForMassUpdate())
            {
                SchedulePostRebuildVisibilityRefresh();
                ScheduleVisionMassUpdateRetry();
                return;
            }

            if (forceFullPass)
            {
                ForceVisionCameraRender();
            }

            visionMirror.TryRefresh(boundVisionCamera.targetTexture, 1);

            if (!visionMirror.HasCpuTexture)
            {
                return;
            }

            if (forceFullPass)
            {
                for (int i = 0; i < hideables.Count; i++)
                {
                    ApplyVisibility(hideables[i]);
                }

                return;
            }

            UpdateHideablesRoundRobin();
        }

        /// <summary>
        /// Mục tiêu: Vision RT cập nhật trước ReadPixels — fog camera không nằm trong stack Main Camera.
        /// Cách hoạt động: Gọi Camera.Render() khi camera active và có targetTexture.
        /// </summary>
        void ForceVisionCameraRender()
        {
            if (boundVisionCamera == null
                || !boundVisionCamera.isActiveAndEnabled
                || boundVisionCamera.targetTexture == null)
            {
                return;
            }

            boundVisionCamera.Render();
        }

        /// <summary>
        /// Mục tiêu: Tránh ẩn 1184 supply khi vision RT còn đen (unit chưa spawn / layer chưa sync).
        /// Cách hoạt động: Cần VisionTransform active của local owner và sample R trên ngưỡng sau Render().
        /// </summary>
        bool TryIsVisionRtReadyForMassUpdate()
        {
            if (!TryGetLocalVisionEmitterState(out bool hasEmitter, out float sampleR))
            {
                return false;
            }

            if (!hasEmitter)
            {
                return false;
            }

            ForceVisionCameraRender();
            visionMirror.TryRefresh(boundVisionCamera.targetTexture, 1);

            if (!visionMirror.HasCpuTexture)
            {
                return false;
            }

            return sampleR > VisionReadyThreshold;
        }

        /// <summary>
        /// Mục tiêu: Một lần quét scene cho emitter + sample R (tránh 2× FindObjects mỗi retry frame).
        /// </summary>
        bool TryGetLocalVisionEmitterState(out bool hasEmitter, out float sampleR)
        {
            hasEmitter = false;
            sampleR = 0f;

            if (_cachedLocalVisionSampleRoot != null)
            {
                hasEmitter = true;
                sampleR = visionMirror.SampleWorldChannel(
                    boundVisionCamera,
                    _cachedLocalVisionSampleRoot.position,
                    requireInsideUv: true);
                return true;
            }

            AbstractCommandable[] commandables = FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable commandable = commandables[i];
                if (commandable == null || commandable.Owner != localOwner)
                {
                    continue;
                }

                Transform visionRoot = commandable.VisionTransformRoot;
                if (visionRoot == null || !visionRoot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _cachedLocalVisionSampleRoot = visionRoot;
                hasEmitter = true;
                sampleR = visionMirror.SampleWorldChannel(
                    boundVisionCamera,
                    visionRoot.position,
                    requireInsideUv: true);
                return true;
            }

            return false;
        }

        void MaybePruneHideables()
        {
            int frame = Time.frameCount;
            if (lastPruneFrame >= 0 && frame - lastPruneFrame < pruneHideablesIntervalFrames)
            {
                return;
            }

            lastPruneFrame = frame;
            for (int i = hideables.Count - 1; i >= 0; i--)
            {
                IHideable hideable = hideables[i];
                if (hideable == null || hideable.Transform == null)
                {
                    if (hideable != null)
                    {
                        hideableLookup.Remove(hideable);
                    }

                    hideables.RemoveAt(i);
                }
            }

            if (hideableCursor >= hideables.Count)
            {
                hideableCursor = 0;
            }
        }

        void UpdateHideablesRoundRobin()
        {
            int count = hideables.Count;
            if (count == 0 || !visionMirror.HasCpuTexture)
            {
                return;
            }

            int budget = Mathf.Min(hideablesUpdatedPerFrame, count);
            for (int n = 0; n < budget; n++)
            {
                if (hideableCursor >= count)
                {
                    hideableCursor = 0;
                }

                ApplyVisibility(hideables[hideableCursor]);
                hideableCursor++;
            }
        }

        void ApplyVisibility(IHideable hideable)
        {
            if (hideable == null || hideable.Transform == null)
            {
                return;
            }

            if (hideable is BaseBuilding building
                && (building.MainRenderer == null
                    || building.Progress.State != BuildingProgress.BuildingState.Completed))
            {
                return;
            }

            bool visibleNow = visionMirror.SampleWorldVisible(
                boundVisionCamera,
                hideable.Transform.position,
                VisionThreshold,
                requireInsideUv: true);

            bool visible = visibleNow;
            if (!visibleNow
                && hideable is GatherableSupply
                && FactionFogSystemsRegistry.TryGet(localOwner, out IFogMapQuery fogQuery))
            {
                visible = fogQuery.IsWorldExplored(hideable.Transform.position);
            }

            hideable.SetVisible(visible);
        }

        void ResolveLocalOwner()
        {
            if (localHumanOwnerService == null)
            {
                localHumanOwnerService = LocalHumanOwnerService.Instance;
            }

            if (localHumanOwnerService != null && localHumanOwnerService.IsInitialized)
            {
                BindLocalOwner(localHumanOwnerService.LocalOwner);
            }
        }

        bool ShouldTrackAsHideable(Owner entityOwner) => entityOwner != localOwner;

        void TrackHideable(IHideable hideable)
        {
            if (hideable == null || !hideableLookup.Add(hideable))
            {
                return;
            }

            hideables.Add(hideable);
        }

        void UntrackHideable(IHideable hideable)
        {
            if (hideable == null)
            {
                return;
            }

            hideableLookup.Remove(hideable);
            hideables.Remove(hideable);
        }

        void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (ShouldTrackAsHideable(evt.Unit.Owner))
            {
                TrackHideable(evt.Unit);
            }
        }

        void HandleUnitDeath(UnitDeathEvent evt) => UntrackHideable(evt.Unit);

        void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (ShouldTrackAsHideable(evt.Building.Owner))
            {
                TrackHideable(evt.Building);
            }
        }

        void HandleBuildingDeath(BuildingDeathEvent evt) => UntrackHideable(evt.Building);
        void HandleSupplySpawn(SupplySpawnEvent evt) => TrackHideable(evt.Supply);
        void HandleSupplyDepleted(SupplyDepletedEvent evt) => UntrackHideable(evt.Supply);
        void HandlePlaceholderDestroy(PlaceholderDestroyEvent evt) => UntrackHideable(evt.Placeholder);
        void HandlePlaceholderSpawn(PlaceholderSpawnEvent evt) => TrackHideable(evt.Placeholder);
    }
}
