using System.Collections;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Sau khi unit/building MP replicate — refresh layer vision + fog plane texture cho local human.
    /// </summary>
    public sealed class MpFogVisionSpawnRefresh : MonoBehaviour
    {
        const int MaxRetryFrames = 10;
        const int VisionLayerRescanIntervalFrames = 12;

        static MpFogVisionSpawnRefresh _instance;
        static bool _retryCoroutineRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => EnsureListener();

        static void EnsureListener()
        {
            if (_instance != null)
            {
                return;
            }

            MpFogVisionSpawnRefresh existing = Object.FindFirstObjectByType<MpFogVisionSpawnRefresh>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            GameObject host = new GameObject(nameof(MpFogVisionSpawnRefresh));
            _instance = host.AddComponent<MpFogVisionSpawnRefresh>();
        }

        /// <summary>
        /// Mục tiêu: P2 client — LocalOwner/unit tới trễ mà không quét cả scene mỗi frame (gây lag).
        /// Cách hoạt động: Một coroutine nhẹ; bỏ qua nếu đã chạy; presentation refresh trước, quét unit thưa.
        /// </summary>
        public static void SchedulePresentationRetries()
        {
            if (_retryCoroutineRunning)
            {
                return;
            }

            EnsureListener();
            _instance?.StartRetryCoroutine();
        }

        void StartRetryCoroutine()
        {
            StopAllCoroutines();
            StartCoroutine(RetryPresentationRefresh());
        }

        IEnumerator RetryPresentationRefresh()
        {
            _retryCoroutineRunning = true;

            try
            {
                int teamIndex = -1;
                for (int frame = 0; frame < MaxRetryFrames; frame++)
                {
                    if (!NetworkClient.active)
                    {
                        yield break;
                    }

                    if (frame == 0)
                    {
                        TryPublishLocalTeamFromLobbyPlayer();
                        teamIndex = MpLocalOwnerSceneSync.ResolveLocalTeamIndex();
                        MpLocalOwnerSceneSync.ApplyTeamAndPresentation(teamIndex);
                        RefreshLocalFactionVisibility(force: true);
                    }

                    RefreshActiveFogPresentation();

                    if (frame % VisionLayerRescanIntervalFrames == 0)
                    {
                        RefreshLocalHumanVisionLayers();
                    }

                    if (frame == MaxRetryFrames - 1)
                    {
                        RefreshLocalFactionVisibility(force: true);
                    }

                    if (IsPresentationReady())
                    {
                        LocalHumanCameraSpawnFocus.RequestRefocusForLocalHuman();
                        yield break;
                    }

                    yield return null;
                }
            }
            finally
            {
                _retryCoroutineRunning = false;
            }
        }

        static void TryPublishLocalTeamFromLobbyPlayer()
        {
            if (NetworkClient.localPlayer == null)
            {
                return;
            }

            RtsLobbyPlayer lobbyPlayer = NetworkClient.localPlayer.GetComponent<RtsLobbyPlayer>();
            if (lobbyPlayer != null)
            {
                RtsLocalHumanOwnerNotifier.NotifyLocalTeamIndex(lobbyPlayer.PlayerTeamIndex);
            }
        }

        static bool IsPresentationReady()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return false;
            }

            MpPlayerPresentationDirector director =
                Object.FindFirstObjectByType<MpPlayerPresentationDirector>(FindObjectsInactive.Include);

            if (director == null || !director.HasConfiguredRigs)
            {
                return false;
            }

            FactionFogPresentation[] presentations = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation != null
                    && presentation.PresentationOwner == service.LocalOwner
                    && presentation.isActiveAndEnabled
                    && presentation.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        static int _lastSyncedLocalUnitCount;

        static void RefreshLocalHumanVisionLayers()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return;
            }

            Owner localOwner = service.LocalOwner;
            int localUnitCount = 0;
            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable commandable = commandables[i];
                if (commandable != null && commandable.Owner == localOwner)
                {
                    commandable.SyncOwnerAndFogVision(localOwner);
                    localUnitCount++;
                }
            }

            if (localUnitCount > _lastSyncedLocalUnitCount)
            {
                RefreshLocalFactionVisibility();
            }

            _lastSyncedLocalUnitCount = localUnitCount;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _retryCoroutineRunning = false;
        }

        void OnEnable()
        {
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
        }

        void OnDisable()
        {
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
        }

        void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit == null)
            {
                return;
            }

            RefreshIfLocal(evt.Unit.Owner, evt.Unit);
        }

        void HandleBuildingSpawn(BuildingSpawnEvent evt) => RefreshIfLocal(evt.Owner, evt.Building);

        void RefreshIfLocal(Owner owner, AbstractCommandable commandable)
        {
            if (commandable != null && HumanFogVisionUtility.IsHumanPlayer(owner))
            {
                commandable.SyncOwnerAndFogVision(owner);
            }

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized || !service.IsLocalOwner(owner))
            {
                return;
            }

            if (service.IsLocalOwner(owner))
            {
                RefreshActiveFogPresentation();
                RefreshLocalFactionVisibility();
            }
        }

        /// <summary>
        /// Mục tiêu: Spawn unit local — refresh visibility có giới hạn, không gọi full Apply presentation.
        /// </summary>
        public static void RequestDebouncedVisibilityRefresh() =>
            RefreshLocalFactionVisibility();

        /// <summary>
        /// Mục tiêu: P2 client — sau SyncOwnerAndFogVision, áp lại visibility từ vision RT (tránh 1184 supply ẩn vĩnh viễn).
        /// Cách hoạt động: Tìm <see cref="FactionVisibilityUpdater"/> trên rig fog đang active của local owner.
        /// </summary>
        public static void RefreshVisibilityForLocalOwner() => RefreshLocalFactionVisibility(force: true);

        static void RefreshLocalFactionVisibility(bool force = false)
        {
            if (!MpFogRefreshThrottle.ShouldRunVisibilityRefresh(force))
            {
                return;
            }

            FactionVisibilityUpdater updater = MpFogRefreshThrottle.ResolveLocalVisibilityUpdater();
            updater?.RefreshVisibilityAfterVisionLayers();
        }

        static void RefreshActiveFogPresentation()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return;
            }

            FactionFogPresentation[] presentations = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation != null && presentation.PresentationOwner == service.LocalOwner)
                {
                    presentation.RefreshFogTextures();
                }
            }
        }
    }
}

