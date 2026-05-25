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

        public void ServerInitSlot(int slotIndex)
        {
            playerTeamIndex = Mathf.Clamp(slotIndex, 0, 1);
        }

        void HookName(string oldV, string newV) { }
        void HookTeam(int oldV, int newV) { }
        void HookReady(bool oldV, bool newV) { }

        public override void OnStartLocalPlayer()
        {
            RtsLobbyUI.Instance?.OnLocalPlayerAssigned(this);
        }

        [Command]
        public void CmdSetReady(bool value)
        {
            ready = value;
        }
    }
}
