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

        public string DisplayName => displayName;
        public int PlayerTeamIndex => playerTeamIndex;
        public bool IsReady => ready;

        public override void OnStartServer()
        {
            if (connectionToClient != null && connectionToClient.authenticationData is string n)
                displayName = n;
            else
                displayName = "Player";
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

        void HookName(string oldV, string newV) { }

        void HookTeam(int oldV, int newV)
        {
            if (isLocalPlayer)
            {
                PublishLocalTeamIndex();
            }
        }

        void HookReady(bool oldV, bool newV) { }

        public override void OnStartLocalPlayer()
        {
            PublishLocalTeamIndex();
            RtsLobbyUI.Instance?.OnLocalPlayerAssigned(this);
        }

        /// <summary>
        /// Mục tiêu: Gán LocalOwner (P1/P2) cho fog/UI/input trên máy local.
        /// Cách hoạt động: Gọi RtsLocalHumanOwnerNotifier với PlayerTeamIndex.
        /// </summary>
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
