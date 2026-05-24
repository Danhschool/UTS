#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// Mục tiêu: Một lần bấm — wire toàn bộ MP (prefab, lobby spawn list, scene game, build settings).
    /// Cách hoạt động: Gọi lần lượt prefab → lobby → game scene → lưu asset/scene.
    /// </summary>
    public static class RtsNetOneClickPlaySetupEditor
    {
        const string CivilCentralPrefabPath = "Assets/Prefab/Buildings/civil_central/civil_central.prefab";
        const string WorkerPrefabPath = "Assets/Prefab/Unit/Worker.prefab";
        const string PlayerPrefabPath = "Assets/3rdParty/RTS_Multiplayer/Prefabs/RtsNet_Player.prefab";
        const string UnitPrefabPath = "Assets/3rdParty/RTS_Multiplayer/Prefabs/RtsNet_Unit.prefab";
        const string LobbyScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity";
        const string GameScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";

        [MenuItem("ProjectRTS/Netplay/★ One-Click Play Setup (ALL — chạy menu này 1 lần)")]
        public static void RunAll()
        {
            if (!EditorUtility.DisplayDialog(
                    "One-Click MP Setup",
                    "Sẽ:\n" +
                    "• Gắn NetworkIdentity + sync trên civil_central & Worker\n" +
                    "• Đăng ký Spawn Prefabs trên NetworkManager (Lobby)\n" +
                    "• Dọn RtsNet_Game (xóa LocalHumanOwnerBootstrap, tắt AI, wire spawn)\n" +
                    "• Build Settings: Lobby + Game\n\n" +
                    "Không copy đè Game 1. Tiếp tục?",
                    "Chạy setup",
                    "Hủy"))
            {
                return;
            }

            RunAllSilent();
            EditorUtility.DisplayDialog(
                "Xong",
                "Setup MP hoàn tất.\n\n" +
                "1. Play scene: RtsNet_Lobby\n" +
                "2. ParrelSync: cửa sổ 2 = Client (127.0.0.1)\n" +
                "3. Host + Client → Ready → Bắt đầu trận",
                "OK");
        }

        [MenuItem("ProjectRTS/Netplay/One-Click Play Setup (silent, no dialog)")]
        public static void RunAllSilent()
        {
            RtsNetUtsIntegrationEditor.EnsureNetworkedPrefabAsset(CivilCentralPrefabPath);
            RtsNetUtsIntegrationEditor.EnsureNetworkedPrefabAsset(WorkerPrefabPath);
            RtsNetUtsIntegrationEditor.EnsurePlayerPrefabCommandsAsset();

            WireLobbyNetworkManager();
            WireGameScene();

            RtsNetPrepareGameSceneEditor.EnsureBuildSettingsPublic();
            AssetDatabase.SaveAssets();

            Debug.Log("[RtsNetOneClick] ★ Setup xong. Play từ RtsNet_Lobby (Host + Client ParrelSync).");
        }

        /// <summary>
        /// Unity batchmode: Unity.exe -batchmode -quit -projectPath "..." -executeMethod GameDevTV.RTS.Editor.Netplay.RtsNetOneClickPlaySetupEditor.ExecuteFromBatch
        /// </summary>
        public static void ExecuteFromBatch() => RunAllSilent();

        static void WireLobbyNetworkManager()
        {
            Scene lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            RtsNetworkManager networkManager = Object.FindFirstObjectByType<RtsNetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[RtsNetOneClick] Không tìm thấy RtsNetworkManager trong RtsNet_Lobby.");
                return;
            }

            var required = new List<GameObject>
            {
                AssetDatabase.LoadAssetAtPath<GameObject>(CivilCentralPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath),
            };

            bool changed = false;
            for (int i = 0; i < required.Count; i++)
            {
                GameObject prefab = required[i];
                if (prefab == null)
                {
                    Debug.LogWarning($"[RtsNetOneClick] Thiếu prefab index {i}.");
                    continue;
                }

                if (!networkManager.spawnPrefabs.Contains(prefab))
                {
                    networkManager.spawnPrefabs.Add(prefab);
                    changed = true;
                }
            }

            if (networkManager.playerPrefab == null)
            {
                networkManager.playerPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(networkManager);
            }

            EditorSceneManager.MarkSceneDirty(lobbyScene);
            EditorSceneManager.SaveScene(lobbyScene);
        }

        [MenuItem("ProjectRTS/Netplay/Fix: Xóa civil_central trong RtsNet_Game (lỗi sceneId Mirror)")]
        public static void FixMirrorSceneIdErrorOnly()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            int removed = RemoveScenePlacedSpawnPrefabs();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "Đã xử lý",
                removed > 0
                    ? $"Đã xóa {removed} instance civil_central/Worker đặt sẵn trong scene.\nMP sẽ spawn khi vào trận."
                    : "Không tìm thấy civil_central/Worker trong scene (có thể đã xóa).",
                "OK");
        }

        static void WireGameScene()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            StripOfflineOnlyComponents();
            RemoveScenePlacedSpawnPrefabs();
            RtsNetUtsIntegrationEditor.WireGameSceneFields();

            if (Object.FindFirstObjectByType<RtsNetGameSceneBootstrap>() == null)
            {
                GameObject bootstrapGo = new GameObject("RtsNetGameSceneBootstrap");
                bootstrapGo.AddComponent<RtsNetGameSceneBootstrap>();
            }

            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>();
            if (setup != null
                && (setup.teamSpawnPoints == null
                    || setup.teamSpawnPoints.Length < 2
                    || setup.teamSpawnPoints[0] == null
                    || setup.teamSpawnPoints[1] == null))
            {
                RtsNetPrepareGameSceneEditor.CreateDefaultSpawnPointsPublic(setup);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
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

            RemoveLegacyFogVisibilityManagers();

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

        /// <summary>
        /// Mục tiêu: Xóa component fog cũ trên scene copy Game 1 mà không reference type Obsolete.
        /// Cách hoạt động: Quét MonoBehaviour, destroy nếu tên type là FogVisibilityManager.
        /// </summary>
        static void RemoveLegacyFogVisibilityManagers()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == "FogVisibilityManager")
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh lỗi Mirror sceneId trên object copy từ Game 1.
        /// Cách hoạt động: Xóa instance trong scene của prefab chỉ spawn runtime (CC + Worker).
        /// </summary>
        static int RemoveScenePlacedSpawnPrefabs()
        {
            var paths = new HashSet<string>
            {
                CivilCentralPrefabPath,
                WorkerPrefabPath,
            };

            int removed = 0;
            NetworkIdentity[] identities = Object.FindObjectsByType<NetworkIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < identities.Length; i++)
            {
                NetworkIdentity identity = identities[i];
                if (identity == null || identity.gameObject.scene.path != GameScenePath)
                {
                    continue;
                }

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject);
                if (source == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(source);
                if (!paths.Contains(assetPath))
                {
                    continue;
                }

                GameObject root = identity.transform.root.gameObject;
                Debug.Log($"[RtsNetOneClick] Xóa khỏi scene (spawn runtime): {root.name}");
                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }
    }
}
#endif
