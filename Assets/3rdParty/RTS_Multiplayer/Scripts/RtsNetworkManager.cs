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
        [Scene] public string loadingScene = "Loading";
        [Scene] public string gameScene = "Game 1";

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
            gameScene = RtsNetSceneUtility.NormalizeSceneName(gameScene);
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ scene gameplay MP từ map host (tên ngắn, không path .unity).
        /// Cách hoạt động: Gán gameScene đã chuẩn hóa cho Mirror ServerChangeScene.
        /// </summary>
        public void SetMatchGameplayScene(string sceneName)
        {
            gameScene = RtsNetSceneUtility.NormalizeSceneName(sceneName);
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

            if (conn != null
                && RtsPlayerSlotRegistry.TryGetSlot(conn.connectionId, out int slot)
                && RtsNetSceneUtility.IsActiveGameplayMapScene())
            {
                RtsServerGameplayNotifier.NotifyPlayerDisconnectedForfeit(slot);
            }

            RtsPlayerSlotRegistry.Release(conn.connectionId);
            base.OnServerDisconnect(conn);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            RtsLocalHumanOwnerNotifier.ClearCachedTeamIndex();
            RtsServerGameplayNotifier.NotifyClientDisconnectedCleanup();
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

            if (lobby != null && slot == 0)
            {
                lobby.ApplyLobbyMapOnServer(
                    RtsLobbyRoomMapSession.PendingIndex,
                    RtsLobbyRoomMapSession.PendingSceneName);
            }
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);

            if (!RtsNetSceneUtility.IsActiveGameplayMapScene())
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
            if (!RtsNetSceneUtility.IsActiveGameplayMapScene())
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

            string targetScene = ResolveMatchGameplayScene();
            SetMatchGameplayScene(targetScene);
            Debug.Log($"[RtsNetworkManager] Bắt đầu trận → scene '{targetScene}'.", this);
            ServerChangeScene(targetScene);
        }

        /// <summary>
        /// Mục tiêu: Lấy map gameplay host đã chọn (SyncVar), không dùng giá trị Inspector cũ.
        /// Cách hoạt động: Ưu tiên host player P1, fallback buffer pending rồi gameScene.
        /// </summary>
        string ResolveMatchGameplayScene()
        {
            if (RtsLobbyRoomMapSync.TryGetHostLobbyPlayer(out RtsLobbyPlayer hostPlayer)
                && !string.IsNullOrWhiteSpace(hostPlayer.LobbyGameplayScene))
            {
                return hostPlayer.LobbyGameplayScene;
            }

            if (!string.IsNullOrWhiteSpace(RtsLobbyRoomMapSession.PendingSceneName))
            {
                return RtsLobbyRoomMapSession.PendingSceneName;
            }

            return gameScene;
        }

        /// <summary>
        /// Mục tiêu: Host chuyển từ Loading scene sang game (Phase A xong).
        /// </summary>
        public void ServerChangeSceneFromLoading(string targetScene)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            base.ServerChangeScene(targetScene);
        }

        public override void ServerChangeScene(string newSceneName)
        {
            if (newSceneName == gameScene
                && !string.IsNullOrWhiteSpace(loadingScene)
                && !IsActiveScene(loadingScene))
            {
                RtsNetworkSceneLoadHooks.BeginNetworkGameplayLoad?.Invoke(gameScene);
                base.ServerChangeScene(loadingScene);
                return;
            }

            base.ServerChangeScene(newSceneName);
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            if (newSceneName == loadingScene)
            {
                RtsNetworkSceneLoadHooks.BeginNetworkGameplayLoad?.Invoke(gameScene);
            }

            base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        }

        static bool IsActiveScene(string sceneName) =>
            SceneManager.GetActiveScene().name == sceneName;

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
