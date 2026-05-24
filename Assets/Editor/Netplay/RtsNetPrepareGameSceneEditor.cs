#if UNITY_EDITOR
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using Mirror;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Netplay
{
    /// <summary>
    /// Mục tiêu: Lobby → RtsNet_Game chơi UTS ngay — copy Game 1 làm scene gameplay rồi wire MP.
    /// </summary>
    public static class RtsNetPrepareGameSceneEditor
    {
        const string Game1ScenePath = "Assets/Scenes/Game 1.unity";
        const string GameScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";
        const string LobbyScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity";

        [MenuItem("ProjectRTS/Netplay/Prepare Game Scene (copy Game 1 → RtsNet_Game)")]
        public static void PrepareGameSceneFromGame1()
        {
            if (!EditorUtility.DisplayDialog(
                    "Chuẩn bị RtsNet_Game",
                    "Sẽ COPY toàn bộ scene Game 1 đè lên RtsNet_Game, rồi thêm spawn MP + wire UTS.\n\nTiếp tục?",
                    "Copy và wire",
                    "Hủy"))
            {
                return;
            }

            if (!AssetDatabase.CopyAsset(Game1ScenePath, GameScenePath))
            {
                Debug.LogError("[RtsNetPrepare] Copy scene thất bại. Kiểm tra đường dẫn Game 1 / RtsNet_Game.");
                return;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            StripOfflineOnlyComponents();
            EnsureMpGameplaySetup();
            EnsureBuildSettings();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Debug.Log(
                "[RtsNetPrepare] Xong. Play từ RtsNet_Lobby: Host + Client (ParrelSync) → Ready → Bắt đầu trận.");
        }

        static void StripOfflineOnlyComponents()
        {
            LocalHumanOwnerBootstrap[] bootstraps = Object.FindObjectsByType<LocalHumanOwnerBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bootstraps.Length; i++)
            {
                if (bootstraps[i] != null)
                {
                    Object.DestroyImmediate(bootstraps[i]);
                }
            }

            AIController[] aiControllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < aiControllers.Length; i++)
            {
                if (aiControllers[i] != null)
                {
                    aiControllers[i].enabled = false;
                }
            }
        }

        static void EnsureMpGameplaySetup()
        {
            RtsNetUtsIntegrationEditor.Integrate();

            if (Object.FindFirstObjectByType<RtsNetGameSceneBootstrap>() == null)
            {
                GameObject bootstrapGo = new GameObject("RtsNetGameSceneBootstrap");
                bootstrapGo.AddComponent<RtsNetGameSceneBootstrap>();
            }

            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>();
            if (setup == null)
            {
                return;
            }

            if (setup.teamSpawnPoints == null || setup.teamSpawnPoints.Length < 2
                || setup.teamSpawnPoints[0] == null || setup.teamSpawnPoints[1] == null)
            {
                CreateDefaultSpawnPoints(setup);
            }
        }

        static void CreateDefaultSpawnPoints(RtsUtsGameSceneSetup setup)
        {
            GameObject root = setup.gameObject;
            root.name = "RtsUtsGameSceneSetup";

            Transform team0 = new GameObject("Team0Spawn").transform;
            team0.SetParent(root.transform);
            team0.position = new Vector3(-40f, 0f, -40f);

            Transform team1 = new GameObject("Team1Spawn").transform;
            team1.SetParent(root.transform);
            team1.position = new Vector3(40f, 0f, 40f);

            setup.teamSpawnPoints = new[] { team0, team1 };
            setup.disableAiControllersOnLoad = true;
        }

        static void EnsureBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            EditorBuildSettings.scenes = scenes;
        }
    }
}
#endif
