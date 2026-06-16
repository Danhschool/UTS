using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Gameplay;
using GameDevTV.RTS.Player;
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
        /// Mục tiêu: Client MP nhận S/W/F khi RtsUtsSupplyStateSync chưa replicate (scene chưa Prepare).
        /// Cách hoạt động: Server gọi ClientRpc trên object scene GameplayMapCore — client luôn có.
        /// </summary>
        public static void BroadcastSupplySnapshot(
            Owner owner,
            int stone,
            int wood,
            int food,
            int population,
            int populationLimit)
        {
            if (!NetworkServer.active || Instance == null)
            {
                return;
            }

            Instance.ServerBroadcastSupplySnapshot(owner, stone, wood, food, population, populationLimit);
        }

        /// <summary>
        /// Mục tiêu: Client MP thấy popup "+N" khi worker gather (server SupplyEvent).
        /// Cách hoạt động: ClientRpc tới HUD local — không dùng SupplyEvent trên pure client.
        /// </summary>
        public static void BroadcastSupplyGain(Owner owner, SupplyGainKind kind, int amount)
        {
            if (!NetworkServer.active || Instance == null || amount <= 0 || kind == SupplyGainKind.Unknown)
            {
                return;
            }

            Instance.ServerBroadcastSupplyGain(owner, kind, amount);
        }

        /// <summary>
        /// Mục tiêu: Pure client P2 gỡ ghost đặt nhà khi server bắt đầu construction.
        /// Cách hoạt động: ClientRpc raise BuildingConstructStartedEvent trên bus localOwner.
        /// </summary>
        public static void BroadcastBuildingConstructStarted(Owner owner)
        {
            if (!NetworkServer.active || Instance == null)
            {
                return;
            }

            Instance.ServerBroadcastBuildingConstructStarted(owner);
        }

        [Server]
        void ServerBroadcastBuildingConstructStarted(Owner owner)
        {
            RpcNotifyBuildingConstructStarted((byte)owner);
        }

        [ClientRpc]
        void RpcNotifyBuildingConstructStarted(byte ownerByte)
        {
            if (isServer)
            {
                return;
            }

            Owner owner = (Owner)ownerByte;
            Owner local = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (owner != local)
            {
                return;
            }

            Bus<BuildingConstructStartedEvent>.Raise(owner, new BuildingConstructStartedEvent(owner));
        }

        [Server]
        void ServerBroadcastSupplyGain(Owner owner, SupplyGainKind kind, int amount)
        {
            RpcNotifySupplyGain((byte)owner, (byte)kind, amount);
        }

        [ClientRpc]
        void RpcNotifySupplyGain(byte ownerByte, byte kindByte, int amount)
        {
            if (isServer || RtsNetplaySession.IsPureClient)
            {
                // Host: popup qua Supplies.HandleSupplyEvent. Pure client: qua snapshot delta trong ApplyNetworkSnapshot.
                return;
            }

            SupplyGainHudPresenter.NotifyGainFromNetwork(
                (Owner)ownerByte,
                (SupplyGainKind)kindByte,
                amount);
        }

        [Server]
        void ServerBroadcastSupplySnapshot(
            Owner owner,
            int stone,
            int wood,
            int food,
            int population,
            int populationLimit)
        {
            RpcApplySupplySnapshot((byte)owner, stone, wood, food, population, populationLimit);
        }

        [ClientRpc]
        void RpcApplySupplySnapshot(
            byte ownerByte,
            int stone,
            int wood,
            int food,
            int population,
            int populationLimit)
        {
            if (isServer)
            {
                return;
            }

            Owner owner = (Owner)ownerByte;
            Supplies.EnsureReady();
            Supplies.ApplyNetworkSnapshot(owner, stone, wood, food, population, populationLimit);
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
