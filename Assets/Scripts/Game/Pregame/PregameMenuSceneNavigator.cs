using GameDevTV.RTS.Game.Startup;
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

        /// <summary>
        /// Mục tiêu: MainMenu → màn chọn map / độ khó.
        /// Cách hoạt động: Lưu PlayMode rồi LoadScene(SSScene).
        /// </summary>
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
        /// Cách hoạt động: Gọi GameplaySceneLoader → Loading → scene game.
        /// </summary>
        public static void StartGameplay(string sceneName)
        {
            GameplaySceneLoader.RequestLoad(sceneName);
        }
    }
}
