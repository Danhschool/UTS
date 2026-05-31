using System.Collections.Generic;
using System.IO;
using GameDevTV.RTS.Game.Startup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Startup.Editor
{
    /// <summary>
    /// SRP: Tạo Loading.unity và cập nhật Build Settings cho luồng load scene.
    /// </summary>
    public static class GameplayLoadingSceneSetupEditor
    {
        const string LoadingScenePath = "Assets/Scenes/Loading.unity";
        const string Game1ScenePath = "Assets/Scenes/Game 1.unity";
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string RtsLobbyPath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity";
        const string RtsGamePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";

        [MenuItem("ProjectRTS/Game/★ Setup Loading Scene + Build Settings")]
        public static void SetupLoadingSceneAndBuildSettings()
        {
            CreateOrUpdateLoadingScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[GameplayLoadingSceneSetupEditor] Xong. Mở Assets/Scenes/Loading.unity, thêm scene vào Build Settings, " +
                "gán loadingScene trên RtsNetworkManager, xóa object Load cũ trong Game 1 nếu còn.");
        }

        [MenuItem("ProjectRTS/Game/Open Loading Scene")]
        public static void OpenLoadingScene()
        {
            if (!File.Exists(LoadingScenePath))
            {
                CreateOrUpdateLoadingScene();
            }

            EditorSceneManager.OpenScene(LoadingScenePath);
        }

        static void CreateOrUpdateLoadingScene()
        {
            Scene scene;
            if (File.Exists(LoadingScenePath))
            {
                scene = EditorSceneManager.OpenScene(LoadingScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            if (Object.FindFirstObjectByType<GameplayLoadingSceneController>() == null)
            {
                var root = new GameObject(nameof(GameplayLoadingSceneController));
                root.AddComponent<GameplayLoadingSceneController>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LoadingScenePath);
        }

        static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                MakeScene(MainMenuScenePath, true),
                MakeScene(LoadingScenePath, true),
                MakeScene(Game1ScenePath, true),
                MakeScene(RtsLobbyPath, true),
                MakeScene(RtsGamePath, true)
            };

            scenes.RemoveAll(s => s == null);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static EditorBuildSettingsScene MakeScene(string path, bool enabled)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GameplayLoadingSceneSetupEditor] Bỏ qua — không tìm thấy: {path}");
                return null;
            }

            return new EditorBuildSettingsScene(path, enabled);
        }
    }
}
