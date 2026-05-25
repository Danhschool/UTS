using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.PvAI;
using System.Collections;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Đặt cameraTarget tại spawn Civil Central của phe local (spawn point → vị trí CC khi replicate).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class LocalHumanCameraSpawnFocus : MonoBehaviour
    {
        [SerializeField] PlayerInput playerInput;

        Owner _subscribedBusOwner = Owner.Invalid;
        bool _snappedToLocalCivilCentral;
        Coroutine _focusRetryCoroutine;

        const int FocusRetryFrameCountOffline = 90;
        const int FocusRetryFrameCountMultiplayer = 240;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOnPlayerInput()
        {
            PlayerInput input = FindPrimaryPlayerInput();
            if (input == null || input.GetComponent<LocalHumanCameraSpawnFocus>() != null)
            {
                return;
            }

            input.gameObject.AddComponent<LocalHumanCameraSpawnFocus>();
        }

        /// <summary>
        /// Mục tiêu: Sau MP presentation (P2) — ép snap lại cameraTarget đúng spawn/CC.
        /// Cách hoạt động: Tìm focus trên shared PlayerInput; reset snap và chạy retry.
        /// </summary>
        public static void RequestRefocusForLocalHuman()
        {
            PlayerInput input = FindPrimaryPlayerInput();
            if (input == null)
            {
                return;
            }

            LocalHumanCameraSpawnFocus focus = input.GetComponent<LocalHumanCameraSpawnFocus>();
            if (focus == null)
            {
                focus = input.gameObject.AddComponent<LocalHumanCameraSpawnFocus>();
            }

            focus.RequestRefocus();
        }

        public void RequestRefocus()
        {
            _snappedToLocalCivilCentral = false;
            ResolvePlayerInput(forceRefresh: true);
            TrySubscribeBusAndFocusSpawnPoint();
            StartFocusRetry();
        }

        void Awake()
        {
            ResolvePlayerInput(forceRefresh: true);
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
            RtsMatchSceneClientNotifier.OnGameSceneLoaded += OnMatchSceneLoaded;
            TrySubscribeBusAndFocusSpawnPoint();
            StartFocusRetry();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            RtsMatchSceneClientNotifier.OnGameSceneLoaded -= OnMatchSceneLoaded;
            StopFocusRetry();
            UnsubscribeBus();
        }

        void OnLocalOwnerChanged(Owner owner)
        {
            _snappedToLocalCivilCentral = false;
            TrySubscribeBusAndFocusSpawnPoint();
            StartFocusRetry();
        }

        void OnMatchSceneLoaded()
        {
            _snappedToLocalCivilCentral = false;
            TrySubscribeBusAndFocusSpawnPoint();
            StartFocusRetry();
        }

        void TrySubscribeBusAndFocusSpawnPoint()
        {
            UnsubscribeBus();
            if (!TryResolveLocalOwner(out Owner localOwner))
            {
                return;
            }

            _subscribedBusOwner = localOwner;
            Bus<BuildingSpawnEvent>.OnEvent[localOwner] += HandleBuildingSpawn;
            TryFocusCamera();
        }

        /// <summary>
        /// Mục tiêu: MP client — owner/CC/presentation tới trễ; vẫn snap cameraTarget đúng chỗ.
        /// Cách hoạt động: Retry theo frame count MP; dừng khi đã snap vào CC thật.
        /// </summary>
        void StartFocusRetry()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopFocusRetry();
            _focusRetryCoroutine = StartCoroutine(FocusRetryRoutine());
        }

        void StopFocusRetry()
        {
            if (_focusRetryCoroutine == null)
            {
                return;
            }

            StopCoroutine(_focusRetryCoroutine);
            _focusRetryCoroutine = null;
        }

        IEnumerator FocusRetryRoutine()
        {
            int maxFrames = NetworkClient.active
                ? FocusRetryFrameCountMultiplayer
                : FocusRetryFrameCountOffline;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                if (_snappedToLocalCivilCentral)
                {
                    yield break;
                }

                ResolvePlayerInput(forceRefresh: frame % 30 == 0);
                TryFocusCamera();
                yield return null;
            }

            _focusRetryCoroutine = null;
        }

        void TryFocusCamera()
        {
            if (!TryResolveLocalOwner(out Owner localOwner) || !ResolvePlayerInput())
            {
                return;
            }

            if (TryFindLocalCivilCentralPosition(localOwner, out Vector3 civilCentralPosition))
            {
                _snappedToLocalCivilCentral = true;
                PanTo(civilCentralPosition);
                StopFocusRetry();
                return;
            }

            if (TryResolveTeamSpawnPosition(localOwner, out Vector3 spawnPosition))
            {
                PanTo(spawnPosition);
            }
        }

        /// <summary>
        /// Mục tiêu: CC đã replicate — snap ngay không đợi event.
        /// Cách hoạt động: Quét BaseBuilding; lọc local owner + Civil Central.
        /// </summary>
        static bool TryFindLocalCivilCentralPosition(Owner localOwner, out Vector3 position)
        {
            position = default;
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null
                    || building.Owner != localOwner
                    || !CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                position = building.transform.position;
                return true;
            }

            return false;
        }

        void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (!TryResolveLocalOwner(out Owner localOwner) || evt.Owner != localOwner)
            {
                return;
            }

            if (evt.Building == null || !CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            _snappedToLocalCivilCentral = true;
            PanTo(evt.Building.transform.position);
            StopFocusRetry();
        }

        /// <summary>
        /// Mục tiêu: Di chuyển cameraTarget tới world XZ của CC / spawn point.
        /// Cách hoạt động: Gọi <see cref="PlayerInput.PanCameraToWorldPosition"/> nếu có reference.
        /// </summary>
        void PanTo(Vector3 worldPosition)
        {
            if (!ResolvePlayerInput())
            {
                return;
            }

            playerInput.PanCameraToWorldPosition(worldPosition);
        }

        bool ResolvePlayerInput(bool forceRefresh = false)
        {
            if (!forceRefresh && playerInput != null && playerInput.CameraTargetTransform != null)
            {
                return true;
            }

            playerInput = GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.CameraTargetTransform != null)
            {
                return true;
            }

            MpPlayerPresentationDirector director =
                Object.FindFirstObjectByType<MpPlayerPresentationDirector>(FindObjectsInactive.Include);

            PlayerInput shared = director != null ? director.SharedPlayerInput : null;
            if (shared != null && shared.CameraTargetTransform != null)
            {
                playerInput = shared;
                return true;
            }

            playerInput = FindPrimaryPlayerInput();
            return playerInput != null && playerInput.CameraTargetTransform != null;
        }

        /// <summary>
        /// Mục tiêu: Tránh gắn focus vào PlayerInput stub P2 (không có cameraTarget).
        /// Cách hoạt động: Ưu tiên enabled + có CameraTargetTransform; fallback bất kỳ có target.
        /// </summary>
        static PlayerInput FindPrimaryPlayerInput()
        {
            PlayerInput[] inputs = Object.FindObjectsByType<PlayerInput>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            PlayerInput fallbackWithTarget = null;
            PlayerInput fallbackAny = null;

            for (int i = 0; i < inputs.Length; i++)
            {
                PlayerInput input = inputs[i];
                if (input == null)
                {
                    continue;
                }

                fallbackAny ??= input;

                if (input.CameraTargetTransform == null)
                {
                    continue;
                }

                fallbackWithTarget ??= input;

                if (input.isActiveAndEnabled)
                {
                    return input;
                }
            }

            return fallbackWithTarget != null ? fallbackWithTarget : fallbackAny;
        }

        static bool TryResolveLocalOwner(out Owner localOwner)
        {
            localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            return HumanFogVisionUtility.IsHumanPlayer(localOwner);
        }

        /// <summary>
        /// Mục tiêu: Lấy vị trí team spawn (trùng chỗ CC sẽ spawn) trước khi building replicate.
        /// Cách hoạt động: teamSpawnPoints MP hoặc factionSpawnPoints PvAI theo Owner/team index.
        /// </summary>
        static bool TryResolveTeamSpawnPosition(Owner localOwner, out Vector3 position)
        {
            position = default;
            int teamIndex = ResolveTeamIndexForOwner(localOwner);
            if (teamIndex < 0)
            {
                return false;
            }

            RtsUtsGameSceneSetup utsSetup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            if (utsSetup != null
                && utsSetup.teamSpawnPoints != null
                && teamIndex < utsSetup.teamSpawnPoints.Length
                && utsSetup.teamSpawnPoints[teamIndex] != null)
            {
                position = utsSetup.teamSpawnPoints[teamIndex].position;
                return true;
            }

            RtsGameSceneSetup legacySetup = Object.FindFirstObjectByType<RtsGameSceneSetup>(FindObjectsInactive.Include);
            if (legacySetup != null
                && legacySetup.teamSpawnPoints != null
                && teamIndex < legacySetup.teamSpawnPoints.Length
                && legacySetup.teamSpawnPoints[teamIndex] != null)
            {
                position = legacySetup.teamSpawnPoints[teamIndex].position;
                return true;
            }

            PvAiGameSceneSetup pvAiSetup = Object.FindFirstObjectByType<PvAiGameSceneSetup>(FindObjectsInactive.Include);
            if (pvAiSetup != null
                && pvAiSetup.factionSpawnPoints != null
                && teamIndex < pvAiSetup.factionSpawnPoints.Length
                && pvAiSetup.factionSpawnPoints[teamIndex] != null)
            {
                position = pvAiSetup.factionSpawnPoints[teamIndex].position;
                return true;
            }

            return false;
        }

        static int ResolveTeamIndexForOwner(Owner localOwner)
        {
            if (NetworkClient.active)
            {
                int team = MpLocalOwnerSceneSync.ResolveLocalTeamIndex();
                if (team >= 0)
                {
                    return team;
                }
            }

            return localOwner switch
            {
                Owner.Player1 => 0,
                Owner.Player2 => 1,
                _ => -1,
            };
        }

        void UnsubscribeBus()
        {
            if (_subscribedBusOwner == Owner.Invalid)
            {
                return;
            }

            Bus<BuildingSpawnEvent>.OnEvent[_subscribedBusOwner] -= HandleBuildingSpawn;
            _subscribedBusOwner = Owner.Invalid;
        }
    }
}
