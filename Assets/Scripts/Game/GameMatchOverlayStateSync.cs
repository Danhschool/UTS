using GameDevTV.RTS.Gameplay;
using GameDevTV.RTS.UI.InGame;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Đồng bộ overlay in-game (tốc độ, pause, panel, đầu hàng) cho cả hai client MP.
    /// Settings chỉ local — không gửi qua lớp này.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class GameMatchOverlayStateSync : NetworkBehaviour
    {
        public static GameMatchOverlayStateSync Instance { get; private set; }

        [SyncVar(hook = nameof(HookSharedPanel))]
        byte sharedPanel;

        [SyncVar(hook = nameof(HookSharedPaused))]
        bool sharedPaused;

        [SyncVar(hook = nameof(HookSharedSpeedScale))]
        float sharedSpeedScale = 1f;

        [SyncVar(hook = nameof(HookSharedSpeedPresetIndex))]
        int sharedSpeedPresetIndex = 1;

        [SyncVar(hook = nameof(HookSharedSurrenderDialog))]
        bool sharedSurrenderDialog;

        public static bool IsNetworkMatchActive => NetworkClient.active || NetworkServer.active;

        static bool s_serverMatchEndSent;

        public static bool HasMatchEndBeenBroadcast => s_serverMatchEndSent;

        /// <summary>Mục tiêu: Trận MP mới — cho phép broadcast kết thúc lại.</summary>
        public static void ResetMatchEndBroadcast()
        {
            s_serverMatchEndSent = false;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameMatchOverlayStateSync] Trùng instance — giữ object đầu tiên.");
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Mục tiêu: Gắn sync lên GameplayMapCore khi server vào scene trận.
        /// Cách hoạt động: Tìm component có sẵn; nếu thiếu thì thêm NetworkIdentity + sync lên core.
        /// </summary>
        public static void EnsureServerInstance()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (Instance != null)
            {
                return;
            }

            GameMatchOverlayStateSync existing = Object.FindFirstObjectByType<GameMatchOverlayStateSync>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            GameplayMapCoreMarker core = Object.FindFirstObjectByType<GameplayMapCoreMarker>(
                FindObjectsInactive.Include);
            GameObject host = core != null ? core.gameObject : new GameObject("GameMatchOverlayStateSync");

            if (host.GetComponent<NetworkIdentity>() == null)
            {
                host.AddComponent<NetworkIdentity>();
            }

            if (host.GetComponent<GameMatchOverlayStateSync>() == null)
            {
                host.AddComponent<GameMatchOverlayStateSync>();
            }

            if (host.GetComponent<NetworkIdentity>().netId == 0)
            {
                NetworkServer.Spawn(host);
            }
        }

        public static void RequestSharedPanel(byte panel, bool paused, bool surrenderDialog)
        {
            if (!IsNetworkMatchActive)
            {
                return;
            }

            if (Instance != null)
            {
                Instance.CmdSetSharedPanel(panel, paused, surrenderDialog);
            }
        }

        public static void RequestSharedSpeed(int presetIndex, float speedScale)
        {
            if (!IsNetworkMatchActive)
            {
                return;
            }

            if (Instance != null)
            {
                Instance.CmdSetSharedSpeed(presetIndex, speedScale);
            }
        }

        public static void RequestSurrenderConfirmed()
        {
            if (!IsNetworkMatchActive)
            {
                return;
            }

            if (Instance != null)
            {
                Instance.CmdConfirmSurrender();
            }
        }

        /// <summary>
        /// Mục tiêu: Server báo cả hai client kết thúc trận khi Civil Central bị phá.
        /// </summary>
        public static void RequestMatchEndFromCivilCentralDestroyed(Owner destroyedOwner)
        {
            if (!IsNetworkMatchActive || Instance == null)
            {
                return;
            }

            if (NetworkServer.active)
            {
                Instance.ServerBroadcastMatchEnd(destroyedOwner, fromDisconnect: false);
            }
        }

        /// <summary>
        /// Mục tiêu: Kết thúc trận khi một người disconnect — phe còn lại thắng.
        /// Cách hoạt động: Server gọi Rpc trực tiếp (không qua Command — hook chạy trên server).
        /// </summary>
        public static void RequestMatchEndFromPlayerDisconnect(Owner disconnectedOwner)
        {
            if (!IsNetworkMatchActive || Instance == null)
            {
                return;
            }

            if (NetworkServer.active)
            {
                Instance.ServerBroadcastMatchEnd(disconnectedOwner, fromDisconnect: true);
            }
        }

        /// <summary>
        /// Mục tiêu: Phát Rpc kết thúc trận từ server — tránh Command khi client đã ngắt.
        /// Cách hoạt động: Chống trùng s_serverMatchEndSent rồi Rpc tới mọi client còn kết nối.
        /// </summary>
        [Server]
        void ServerBroadcastMatchEnd(Owner destroyedOwner, bool fromDisconnect)
        {
            if (s_serverMatchEndSent)
            {
                return;
            }

            s_serverMatchEndSent = true;
            RpcMatchEndFromCivilCentralDestroyed(destroyedOwner);
        }

        [Command(requiresAuthority = false)]
        void CmdSetSharedPanel(byte panel, bool paused, bool surrenderDialog)
        {
            sharedPanel = panel;
            sharedPaused = paused;
            sharedSurrenderDialog = surrenderDialog;
            ApplyStateToAllControllers();
        }

        [Command(requiresAuthority = false)]
        void CmdSetSharedSpeed(int presetIndex, float speedScale)
        {
            sharedSpeedPresetIndex = Mathf.Clamp(presetIndex, 0, 3);
            sharedSpeedScale = Mathf.Max(0.0001f, speedScale);
            sharedPanel = 0;
            sharedPaused = false;
            sharedSurrenderDialog = false;
            ApplyStateToAllControllers();
        }

        [Command(requiresAuthority = false)]
        void CmdConfirmSurrender()
        {
            RpcEndMatchFromSurrender();
        }

        [ClientRpc]
        void RpcEndMatchFromSurrender()
        {
            InGameOverlayMenuController.ApplyRemoteMatchEndFromSurrender();
        }

        [Command(requiresAuthority = false)]
        void CmdMatchEndFromCivilCentralDestroyed(Owner destroyedOwner)
        {
            ServerBroadcastMatchEnd(destroyedOwner, fromDisconnect: false);
        }

        [Command(requiresAuthority = false)]
        void CmdMatchEndFromPlayerDisconnect(Owner disconnectedOwner)
        {
            ServerBroadcastMatchEnd(disconnectedOwner, fromDisconnect: true);
        }

        [ClientRpc]
        void RpcMatchEndFromCivilCentralDestroyed(Owner destroyedOwner)
        {
            MatchOutcomeDetector.ApplyNetworkCivilCentralDestroyed(destroyedOwner);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isServer)
            {
                ApplyStateToAllControllers();
            }
        }

        void HookSharedPanel(byte oldValue, byte newValue) => ApplyStateToAllControllers();

        void HookSharedPaused(bool oldValue, bool newValue) => ApplyStateToAllControllers();

        void HookSharedSpeedScale(float oldValue, float newValue) => ApplyStateToAllControllers();

        void HookSharedSpeedPresetIndex(int oldValue, int newValue) => ApplyStateToAllControllers();

        void HookSharedSurrenderDialog(bool oldValue, bool newValue) => ApplyStateToAllControllers();

        void ApplyStateToAllControllers()
        {
            InGameOverlayMenuController.ApplyRemoteSharedState(
                sharedPanel,
                sharedPaused,
                sharedSpeedPresetIndex,
                sharedSpeedScale,
                sharedSurrenderDialog);
        }

        public byte SharedPanel => sharedPanel;
        public bool SharedPaused => sharedPaused;
        public float SharedSpeedScale => sharedSpeedScale;
        public int SharedSpeedPresetIndex => sharedSpeedPresetIndex;
        public bool SharedSurrenderDialog => sharedSurrenderDialog;
    }
}
