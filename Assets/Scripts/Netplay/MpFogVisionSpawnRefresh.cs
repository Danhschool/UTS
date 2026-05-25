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
        const int MaxRetryFrames = 24;
        const int VisionLayerRescanIntervalFrames = 8;

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
                for (int frame = 0; frame < MaxRetryFrames; frame++)
                {
                    if (!NetworkClient.active)
                    {
                        yield break;
                    }

                    TryPublishLocalTeamFromLobbyPlayer();
                    MpLocalOwnerSceneSync.TryApplyTeamIndex(MpLocalOwnerSceneSync.ResolveLocalTeamIndex());
                    LocalHumanPresentationRefresh.RefreshFromLocalOwner();
                    RefreshActiveFogPresentation();

                    if (frame == 0 || frame % VisionLayerRescanIntervalFrames == 0)
                    {
                        RefreshLocalHumanVisionLayers();
                    }

                    if (IsPresentationReady())
                    {
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

        static void RefreshLocalHumanVisionLayers()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return;
            }

            Owner localOwner = service.LocalOwner;
            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable commandable = commandables[i];
                if (commandable != null && commandable.Owner == localOwner)
                {
                    commandable.SyncOwnerAndFogVision(localOwner);
                }
            }
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

            LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            RefreshActiveFogPresentation();
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
