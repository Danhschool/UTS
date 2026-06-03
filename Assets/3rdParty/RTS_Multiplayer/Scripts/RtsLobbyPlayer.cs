using Mirror;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Đại diện người chơi trên mạng (lobby + game): tên, phe, trạng thái Ready.
    /// Cách hoạt động: Server gán slot từ thứ tự kết nối; tên lấy từ authenticator; SyncVar replicate xuống client.
    /// </summary>
    public class RtsLobbyPlayer : NetworkBehaviour
    {
        [SyncVar(hook = nameof(HookName))]
        string displayName;

        [SyncVar(hook = nameof(HookTeam))]
        int playerTeamIndex;

        [SyncVar(hook = nameof(HookReady))]
        bool ready;

        [SyncVar(hook = nameof(HookLobbyMapIndex))]
        int lobbyMapIndex;

        [SyncVar(hook = nameof(HookLobbyGameplayScene))]
        string lobbyGameplayScene = PregameGameplaySceneFallback.DefaultScene;

        public string DisplayName => displayName;
        public int PlayerTeamIndex => playerTeamIndex;
        public bool IsReady => ready;
        public int LobbyMapIndex => lobbyMapIndex;
        public string LobbyGameplayScene => lobbyGameplayScene;

        public override void OnStartServer()
        {
            if (connectionToClient != null && connectionToClient.authenticationData is string n)
                displayName = n;
            else
                displayName = "Player";

            if (playerTeamIndex == 0)
            {
                ApplyLobbyMapOnServer(RtsLobbyRoomMapSession.PendingIndex, RtsLobbyRoomMapSession.PendingSceneName);
            }
        }

        bool _serverSlotApplied;

        public void ServerInitSlot(int slotIndex)
        {
            if (_serverSlotApplied)
            {
                return;
            }

            if (connectionToClient != null)
            {
                slotIndex = RtsPlayerSlotRegistry.AssignOrGet(connectionToClient);
            }

            _serverSlotApplied = true;
            playerTeamIndex = Mathf.Clamp(slotIndex, 0, 1);
            Debug.Log(
                $"[RtsLobbyPlayer] ServerInitSlot → team {playerTeamIndex} ({(playerTeamIndex == 0 ? "Player1" : "Player2")}), conn={connectionToClient?.connectionId}");
        }

        void HookName(string oldV, string newV)
        {
            RtsLobbyUI.Instance?.OnLobbyPlayerStateChanged();
        }

        void HookTeam(int oldV, int newV)
        {
            if (isLocalPlayer)
            {
                PublishLocalTeamIndex();
            }

            RtsLobbyUI.Instance?.OnLobbyPlayerStateChanged();
        }

        void HookReady(bool oldV, bool newV)
        {
            RtsLobbyUI.Instance?.OnLobbyPlayerStateChanged();
        }

        void HookLobbyMapIndex(int oldValue, int newValue)
        {
        }

        void HookLobbyGameplayScene(string oldValue, string newValue)
        {
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (PlayerTeamIndex == 0 && !isLocalPlayer)
            {
                BroadcastLobbyMapToUiClients(lobbyMapIndex, lobbyGameplayScene);
            }
        }

        /// <summary>
        /// Mục tiêu: Host đặt map phòng; client nhận qua ClientRpc (một lần, cặp index+scene đúng).
        /// Cách hoạt động: Server cập nhật SyncVar + Rpc; không notify qua từng SyncVar hook.
        /// </summary>
        public void ApplyLobbyMapOnServer(int index, string sceneName)
        {
            if (!NetworkServer.active || PlayerTeamIndex != 0)
            {
                return;
            }

            lobbyGameplayScene = RtsNetSceneUtility.NormalizeSceneName(sceneName);
            lobbyMapIndex = index < 0 ? 0 : index;
            RtsLobbyRoomMapSession.SetPending(lobbyMapIndex, lobbyGameplayScene);

            if (NetworkManager.singleton is RtsNetworkManager networkManager)
            {
                networkManager.SetMatchGameplayScene(lobbyGameplayScene);
            }

            Debug.Log(
                $"[RtsLobbyPlayer] Host map → index={lobbyMapIndex}, scene='{lobbyGameplayScene}' (conn={connectionToClient?.connectionId}).");

            RpcSyncLobbyMapToClients(lobbyMapIndex, lobbyGameplayScene);
        }

        [ClientRpc]
        void RpcSyncLobbyMapToClients(int index, string sceneName)
        {
            BroadcastLobbyMapToUiClients(index, sceneName);
        }

        /// <summary>
        /// Mục tiêu: Client lobby UI bám map host qua một event đồng bộ (không lệch hook SyncVar).
        /// Cách hoạt động: RaiseMapSelectionChanged; host local player bỏ qua (UI tự chọn map).
        /// </summary>
        static void BroadcastLobbyMapToUiClients(int index, string sceneName)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                return;
            }

            RtsLobbyRoomMapSync.RaiseMapSelectionChanged(index, sceneName);
        }

        public override void OnStartLocalPlayer()
        {
            PublishLocalTeamIndex();
            RtsLobbyUI.Instance?.OnLocalPlayerAssigned(this);
        }

        void PublishLocalTeamIndex()
        {
            RtsLocalHumanOwnerNotifier.NotifyLocalTeamIndex(PlayerTeamIndex);
        }

        [Command]
        public void CmdSetReady(bool value)
        {
            ready = value;
        }
    }
}
