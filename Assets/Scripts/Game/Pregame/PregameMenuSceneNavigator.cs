using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.PvAI;
using Mirror;using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Pregame
{
    /// <summary>
    /// SRP: Chuyển scene menu / setup — không đi qua Loading (chỉ gameplay mới qua Loading).
    /// </summary>
    public static class PregameMenuSceneNavigator
    {
        public const string MainMenuScene = "MainMenu";
        public const string SetupScene = "SSScene";
        public const string LobbyScene = "RtsNet_Lobby";

        public static void LoadMainMenu()
        {
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void LoadSetup(PregamePlayMode mode)
        {
            PregameSessionState.SetPlayMode(mode);
            SceneManager.LoadScene(SetupScene);
        }

        public static void LoadLobby()
        {
            SceneManager.LoadScene(LobbyScene);
        }

        /// <summary>
        /// Mục tiêu: Bắt đầu trận offline từ setup.
        /// Cách hoạt động: Tắt Mirror nếu còn sót → reset owner PvE → Loading → scene game.
        /// </summary>
        public static void StartGameplay(string sceneName)
        {
            EnsureOfflineNetworkStopped();
            RtsLocalHumanOwnerNotifier.ClearCachedTeamIndex();
            PvAiOfflineSessionPrep.OnGameplayLoadRequested();
            PvAiOfflineSessionPrep.PrepareLocalHumanOwner();
            GameplaySceneLoader.RequestLoad(sceneName);
        }

        /// <summary>
        /// Mục tiêu: PvE offline không bị chặn spawn vì session MP trước đó còn active.
        /// Cách hoạt động: StopHost/StopClient trên NetworkManager.singleton nếu đang chạy.
        /// </summary>
        static void EnsureOfflineNetworkStopped()
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                return;
            }

            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null)
            {
                return;
            }

            if (NetworkServer.active && NetworkClient.active)
            {
                networkManager.StopHost();
            }
            else if (NetworkClient.active)
            {
                networkManager.StopClient();
            }
            else if (NetworkServer.active)
            {
                networkManager.StopServer();
            }

            RtsLocalHumanOwnerNotifier.ClearCachedTeamIndex();
            Debug.Log("[PregameMenuSceneNavigator] Đã tắt Mirror trước khi vào PvE offline.");
        }
    }
}
