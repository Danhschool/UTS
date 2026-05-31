using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.PvAI;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Nhận diện scene gameplay (Game*, RtsNet_Game, …) — không áp loading lên menu/lobby.
    /// </summary>
    public static class GameplayStartupScenes
    {
        const string LobbySceneName = "RtsNet_Lobby";
        const string MainMenuSceneName = "MainMenu";
        const string SetupSceneName = "SSScene";
        const string LoadingSceneName = "Loading";

        public static bool IsLoadingScene(Scene scene) =>
            scene.IsValid() && scene.name == LoadingSceneName;

        public static bool IsGameplayScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            if (scene.name == LobbySceneName
                || scene.name == MainMenuSceneName
                || scene.name == SetupSceneName
                || scene.name == LoadingSceneName)
            {
                return false;
            }

            if (scene.name == "RtsNet_Game" || scene.name.StartsWith("Game"))
            {
                return true;
            }

            if (scene != SceneManager.GetActiveScene())
            {
                return false;
            }

            return Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include) != null
                   || Object.FindFirstObjectByType<PvAiGameSceneSetup>(FindObjectsInactive.Include) != null
                   || Object.FindFirstObjectByType<LocalHumanOwnerBootstrap>(FindObjectsInactive.Include) != null
                   || Object.FindFirstObjectByType<RtsNetGameSceneBootstrap>(FindObjectsInactive.Include) != null;
        }

        public static bool IsActiveGameplayScene() =>
            IsGameplayScene(SceneManager.GetActiveScene());
    }
}
