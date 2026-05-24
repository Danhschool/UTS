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
            RefreshAllHideableVisibility(forceFullPass: true);
        }

        public void BindLocalOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            localOwner = owner;
            RebuildHideablesForLocalOwner();
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

            RefreshAllHideableVisibility(forceFullPass: true);
        }

        void LateUpdate()
        {
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

            visionMirror.TryRefresh(boundVisionCamera.targetTexture, 1);

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

            bool visible = visionMirror.SampleWorldVisible(
                boundVisionCamera,
                hideable.Transform.position,
                VisionThreshold,
                requireInsideUv: true);
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
