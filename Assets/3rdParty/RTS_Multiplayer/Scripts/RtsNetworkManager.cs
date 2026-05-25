using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Quản lý session RTS 2 người — giới hạn kết nối, chuyển scene khi đủ người và sẵn sàng, dọn tên khi disconnect.
    /// Cách hoạt động: Kế thừa NetworkManager, gán maxConnections = 2; host bấm bắt đầu khi ServerAllPlayersReady; spawn unit khi vào scene chơi.
    /// </summary>
    [DisallowMultipleComponent]
    public class RtsNetworkManager : NetworkManager
    {
        [Header("RTS scenes (must be in Build Settings)")]
        [Scene] public string lobbyScene = "RtsNet_Lobby";
        [Scene] public string gameScene = "RtsNet_Game";

        [Header("Prefabs")]
        public GameObject unitPrefab;

        /// <summary>Địa chỉ IP hoặc hostname client kết nối tới (đồng bộ UI).</summary>
        public void SetNetworkAddress(string address)
        {
            if (!string.IsNullOrWhiteSpace(address))
                networkAddress = address.Trim();
        }

        public override void Awake()
        {
            base.Awake();
            maxConnections = 2;
            if (string.IsNullOrWhiteSpace(networkAddress))
                networkAddress = "localhost";
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RtsPlayerSlotRegistry.Reset();
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.authenticationData is string name && !string.IsNullOrEmpty(name))
                RtsUniqueNameAuthenticator.PlayerNames.Remove(name);

            RtsLobbyChat.ClearConnectionName(conn);
            RtsPlayerSlotRegistry.Release(conn.connectionId);
            base.OnServerDisconnect(conn);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            RtsLocalHumanOwnerNotifier.ClearCachedTeamIndex();
            RtsLobbyUI.Instance?.ShowLoginAgain();
        }

        public override void OnClientError(TransportError error, string reason)
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[RtsNetworkManager] Client transport: {error} — {reason}");
            base.OnClientError(error, reason);
        }

        public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning($"[RtsNetworkManager] Server transport: {error} — {reason} (conn={conn?.connectionId})");
            base.OnServerError(conn, error, reason);
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Transform start = GetStartPosition();
            Vector3 pos = start != null ? start.position : Vector3.zero;
            Quaternion rot = start != null ? start.rotation : Quaternion.identity;
            GameObject player = Instantiate(playerPrefab, pos, rot);
            var lobby = player.GetComponent<RtsLobbyPlayer>();
            int slot = RtsPlayerSlotRegistry.AssignOrGet(conn);
            if (lobby != null)
            {
                lobby.ServerInitSlot(slot);
            }

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (!RtsNetSceneUtility.MatchesActiveScene(gameScene))
            {
                RtsServerGameplayNotifier.ResetMatchSpawnState();
                return;
            }

            OnEnteredGameplayScene();

            if (NetworkServer.active)
            {
                RtsServerGameplayNotifier.ResetMatchSpawnState();
                RtsServerGameplayNotifier.NotifyMatchSceneLoaded();
            }
        }

        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            OnEnteredGameplayScene();
        }

        void OnEnteredGameplayScene()
        {
            if (!RtsNetSceneUtility.MatchesActiveScene(gameScene))
            {
                return;
            }

            RtsLobbyUI.HideLobbyCanvasForGameplay();
            RtsMatchSceneClientNotifier.NotifyGameSceneLoaded();
        }

        /// <summary>Gọi từ UI host khi cả hai người đã Ready.</summary>
        public void ServerTryStartMatch()
        {
            if (!NetworkServer.active || !NetworkClient.isConnected)
            {
                Debug.LogWarning("[RtsNetworkManager] Chỉ host (server + client) mới bắt đầu trận.");
                return;
            }

            if (!AllPlayersReady())
            {
                Debug.LogWarning("[RtsNetworkManager] Chưa đủ người hoặc chưa Ready.");
                return;
            }

            ServerChangeScene(gameScene);
        }

        bool AllPlayersReady()
        {
            if (NetworkServer.connections.Count < 2)
                return false;
            foreach (var kvp in NetworkServer.connections)
            {
                var c = kvp.Value;
                if (c == null || c.identity == null)
                    return false;
                var lp = c.identity.GetComponent<RtsLobbyPlayer>();
                if (lp == null || !lp.IsReady)
                    return false;
            }
            return true;
        }
    }
}
