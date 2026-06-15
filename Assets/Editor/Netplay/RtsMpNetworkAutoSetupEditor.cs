#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Editor.Gameplay;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Netplay
{
    /// <summary>
    /// SRP: Tự gắn NetworkIdentity + RtsUtsNetworkEntity, đăng ký spawn prefab, wire scene MP.
    /// </summary>
    public static class RtsMpNetworkAutoSetupEditor
    {
        const string LobbyScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity";
        const string PlayerPrefabPath = "Assets/3rdParty/RTS_Multiplayer/Prefabs/RtsNet_Player.prefab";
        const string LegacyUnitPrefabPath = "Assets/3rdParty/RTS_Multiplayer/Prefabs/RtsNet_Unit.prefab";

        static readonly string[] PrefabScanRoots =
        {
            "Assets/Prefab/Unit",
            "Assets/Prefab/Buildings",
        };

        [MenuItem("ProjectRTS/Netplay/★ Auto-Wire MP Prefabs & Scene (one-click)")]
        public static void AutoWireMenu()
        {
            AutoWireAll(saveScenes: true);
            Debug.Log("[RtsMpAutoSetup] Hoàn tất — prefab network + lobby spawn + Game 1 & Game 2 setup.");
        }

        [MenuItem("ProjectRTS/Netplay/Fix Duplicate NetworkIdentity on Prefabs")]
        public static void FixDuplicateNetworkIdentityMenu()
        {
            int cleaned = CleanupAllGameplayPrefabNetworkHosts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RtsMpAutoSetup] Đã dọn NetworkIdentity trùng trên {cleaned} prefab. Clear Console rồi test lại.");
        }

        /// <summary>
        /// Mục tiêu: Xóa NetworkIdentity/RtsUtsNetworkEntity trên xương/mesh con; giữ đúng một bộ trên host gameplay.
        /// Cách hoạt động: LoadPrefabContents → RemoveStray → đảm bảo host có đủ component → SaveAsPrefabAsset.
        /// </summary>
        public static int CleanupAllGameplayPrefabNetworkHosts()
        {
            int processed = 0;
            for (int r = 0; r < PrefabScanRoots.Length; r++)
            {
                string root = PrefabScanRoots[r];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.Contains("ghost", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null || !ShouldReceiveNetworkComponents(asset, path))
                    {
                        continue;
                    }

                    if (CleanupSinglePrefabNetworkHost(path))
                    {
                        processed++;
                    }
                }
            }

            return processed;
        }

        static bool CleanupSinglePrefabNetworkHost(string path)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int removed = RemoveStrayNetworkComponents(prefabRoot);
                GameObject host = ResolveNetworkHost(prefabRoot);
                if (host == null)
                {
                    return removed > 0;
                }

                bool dirty = removed > 0;
                if (!host.TryGetComponent(out NetworkIdentity _))
                {
                    host.AddComponent<NetworkIdentity>();
                    dirty = true;
                }

                if (!host.TryGetComponent(out RtsUtsNetworkEntity _))
                {
                    host.AddComponent<RtsUtsNetworkEntity>();
                    dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[RtsMpAutoSetup] Cleaned network host → {path} ({host.name})");
                }

                return dirty || removed > 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Mục tiêu: Chạy headless từ Unity -batchmode -executeMethod.
        /// Cách hoạt động: Gọi AutoWireAll và lưu scene/prefab.
        /// </summary>
        public static void ExecuteBatch()
        {
            AutoWireAll(saveScenes: true);
            EditorApplication.Exit(0);
        }

        public static void AutoWireAll(bool saveScenes)
        {
            CleanupAllGameplayPrefabNetworkHosts();
            List<GameObject> spawnCandidates = EnsureNetworkOnGameplayPrefabs();
            EnsurePlayerCommandsOnNetworkPlayer();
            WireLobbyNetworkManagerSpawnPrefabs(spawnCandidates);

            for (int m = 0; m < GameplayMapScenePaths.PrimaryGameplayMapScenePaths.Length; m++)
            {
                string mapPath = GameplayMapScenePaths.PrimaryGameplayMapScenePaths[m];
                Scene gameScene = EditorSceneManager.OpenScene(mapPath, OpenSceneMode.Single);
                RtsNetGameSceneSetupEditor.PrepareOpenScene();
                WireUtsGameSceneSetupCatalog(spawnCandidates);

                if (saveScenes)
                {
                    EditorSceneManager.MarkSceneDirty(gameScene);
                    EditorSceneManager.SaveScene(gameScene);
                }
            }

            if (saveScenes)
            {
                Scene lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
                EnsureLobbyDefaultGameplayScene(lobbyScene);
                EditorSceneManager.MarkSceneDirty(lobbyScene);
                EditorSceneManager.SaveScene(lobbyScene);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Mục tiêu: Mọi prefab unit/nhà có thể spawn trong MP đều có NetworkIdentity + RtsUtsNetworkEntity.
        /// Cách hoạt động: Quét UnlockableSO + thư mục Unit/Buildings; bỏ ghost/UI; AddComponent khi thiếu.
        /// </summary>
        public static List<GameObject> EnsureNetworkOnGameplayPrefabs()
        {
            var candidates = new HashSet<GameObject>();
            CollectPrefabsFromUnlockables(candidates);
            CollectPrefabsFromScanRoots(candidates);

            int addedIdentity = 0;
            int addedEntity = 0;
            var ordered = candidates
                .Where(p => p != null)
                .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                GameObject prefab = ordered[i];
                string path = AssetDatabase.GetAssetPath(prefab);
                if (!ShouldReceiveNetworkComponents(prefab, path))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int removed = RemoveStrayNetworkComponents(prefabRoot);
                    if (removed > 0)
                    {
                        Debug.Log($"[RtsMpAutoSetup] Removed {removed} stray network component(s) → {path}");
                    }

                    GameObject host = ResolveNetworkHost(prefabRoot);
                    if (host == null)
                    {
                        continue;
                    }

                    bool dirty = removed > 0;
                    if (!host.TryGetComponent(out NetworkIdentity _))
                    {
                        host.AddComponent<NetworkIdentity>();
                        addedIdentity++;
                        dirty = true;
                        Debug.Log($"[RtsMpAutoSetup] +NetworkIdentity → {path} ({host.name})");
                    }

                    if (!host.TryGetComponent(out RtsUtsNetworkEntity _))
                    {
                        host.AddComponent<RtsUtsNetworkEntity>();
                        addedEntity++;
                        dirty = true;
                        Debug.Log($"[RtsMpAutoSetup] +RtsUtsNetworkEntity → {path} ({host.name})");
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            Debug.Log(
                $"[RtsMpAutoSetup] Prefab network: {ordered.Count} candidate, "
                + $"+{addedIdentity} NetworkIdentity, +{addedEntity} RtsUtsNetworkEntity.");
            return ordered;
        }

        static void CollectPrefabsFromUnlockables(HashSet<GameObject> candidates)
        {
            string[] unlockableGuids = AssetDatabase.FindAssets("t:UnlockableSO", new[] { "Assets" });
            for (int i = 0; i < unlockableGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(unlockableGuids[i]);
                UnlockableSO unlockable = AssetDatabase.LoadAssetAtPath<UnlockableSO>(assetPath);
                if (unlockable == null)
                {
                    continue;
                }

                if (unlockable is BuildingSO buildingSo)
                {
                    TryAddPrefab(candidates, buildingSo.Prefab);
                }
                else if (unlockable is AbstractUnitSO unitSo)
                {
                    TryAddPrefab(candidates, unitSo.Prefab);
                }
            }
        }

        static void CollectPrefabsFromScanRoots(HashSet<GameObject> candidates)
        {
            for (int r = 0; r < PrefabScanRoots.Length; r++)
            {
                string root = PrefabScanRoots[r];
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    TryAddPrefab(candidates, prefab);
                }
            }
        }

        static void TryAddPrefab(HashSet<GameObject> candidates, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            candidates.Add(prefab);
        }

        static bool ShouldReceiveNetworkComponents(GameObject prefab, string path)
        {
            if (prefab == null || string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.Equals(LegacyUnitPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (path.Contains("ghost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (path.Contains("/UI/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Tree/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Rock/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Animal/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (prefab.GetComponent<BaseBuilding>() != null
                || prefab.GetComponent<AbstractUnit>() != null
                || prefab.GetComponent<BaseMilitaryUnit>() != null)
            {
                return true;
            }

            return prefab.GetComponentInChildren<AbstractUnit>(true) != null
                || prefab.GetComponentInChildren<BaseBuilding>(true) != null;
        }

        /// <summary>
        /// Mục tiêu: Mirror chỉ cho phép một NetworkIdentity trên root gameplay của prefab.
        /// Cách hoạt động: Tìm object có BaseBuilding/AbstractUnit; xóa NI/Entity trên child khác.
        /// </summary>
        static GameObject ResolveNetworkHost(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                return null;
            }

            if (prefabRoot.GetComponent<BaseBuilding>() != null
                || prefabRoot.GetComponent<AbstractUnit>() != null
                || prefabRoot.GetComponent<BaseMilitaryUnit>() != null)
            {
                return prefabRoot;
            }

            BaseBuilding building = prefabRoot.GetComponentInChildren<BaseBuilding>(true);
            if (building != null)
            {
                return building.gameObject;
            }

            AbstractUnit unit = prefabRoot.GetComponentInChildren<AbstractUnit>(true);
            return unit != null ? unit.gameObject : prefabRoot;
        }

        static int RemoveStrayNetworkComponents(GameObject prefabRoot)
        {
            GameObject host = ResolveNetworkHost(prefabRoot);
            if (host == null)
            {
                return 0;
            }

            int removed = 0;
            NetworkIdentity[] identities = prefabRoot.GetComponentsInChildren<NetworkIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                NetworkIdentity identity = identities[i];
                if (identity == null || identity.gameObject == host)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(identity);
                removed++;
            }

            RtsUtsNetworkEntity[] entities = prefabRoot.GetComponentsInChildren<RtsUtsNetworkEntity>(true);
            for (int i = 0; i < entities.Length; i++)
            {
                RtsUtsNetworkEntity entity = entities[i];
                if (entity == null || entity.gameObject == host)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(entity);
                removed++;
            }

            return removed;
        }

        static void EnsurePlayerCommandsOnNetworkPlayer()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[RtsMpAutoSetup] Không tìm thấy {PlayerPrefabPath}");
                return;
            }

            if (playerPrefab.GetComponent<RtsUtsPlayerCommands>() == null)
            {
                playerPrefab.AddComponent<RtsUtsPlayerCommands>();
                EditorUtility.SetDirty(playerPrefab);
                Debug.Log("[RtsMpAutoSetup] Đã thêm RtsUtsPlayerCommands → RtsNet_Player.");
            }
        }

        /// <summary>
        /// Mục tiêu: Mirror chỉ spawn prefab đã đăng ký trên NetworkManager.
        /// Cách hoạt động: Mở lobby scene, merge spawnCandidates vào RtsNetworkManager.spawnPrefabs.
        /// </summary>
        static void WireLobbyNetworkManagerSpawnPrefabs(IReadOnlyList<GameObject> spawnCandidates)
        {
            Scene lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            RtsNetworkManager manager = UnityEngine.Object.FindFirstObjectByType<RtsNetworkManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogWarning("[RtsMpAutoSetup] Không tìm thấy RtsNetworkManager trong lobby.");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            int added = 0;
            for (int i = 0; i < spawnCandidates.Count; i++)
            {
                GameObject prefab = spawnCandidates[i];
                if (prefab == null || prefab.GetComponent<NetworkIdentity>() == null)
                {
                    continue;
                }

                if (manager.spawnPrefabs.Contains(prefab))
                {
                    continue;
                }

                manager.spawnPrefabs.Add(prefab);
                added++;
            }

            EnsureLobbyDefaultGameplayScene(lobbyScene, manager);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(lobbyScene);
            Debug.Log($"[RtsMpAutoSetup] NetworkManager.spawnPrefabs: +{added} (tổng {manager.spawnPrefabs.Count}).");
        }

        /// <summary>
        /// Mục tiêu: Fallback MP trước khi host chọn map — mặc định Game 1, không RtsNet_Game.
        /// Cách hoạt động: Gán gameScene trên RtsNetworkManager trong lobby scene.
        /// </summary>
        static void EnsureLobbyDefaultGameplayScene(Scene lobbyScene, RtsNetworkManager manager = null)
        {
            if (manager == null)
            {
                manager = UnityEngine.Object.FindFirstObjectByType<RtsNetworkManager>(FindObjectsInactive.Include);
            }

            if (manager == null)
            {
                return;
            }

            string defaultScene = System.IO.Path.GetFileNameWithoutExtension(
                GameplayMapScenePaths.Game1ScenePath);
            if (manager.gameScene != defaultScene)
            {
                manager.gameScene = defaultScene;
                Debug.Log($"[RtsMpAutoSetup] NetworkManager.gameScene → '{defaultScene}'.");
            }
        }

        /// <summary>
        /// Mục tiêu: Server MP resolve train/build/research và factory spawn đúng catalog.
        /// Cách hoạt động: Gán unlockableCatalog + additionalNetworkSpawnPrefabs trên RtsUtsGameSceneSetup.
        /// </summary>
        static void WireUtsGameSceneSetupCatalog(IReadOnlyList<GameObject> spawnCandidates)
        {
            RtsUtsGameSceneSetup setup = UnityEngine.Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            if (setup == null)
            {
                Debug.LogWarning("[RtsMpAutoSetup] Scene thiếu RtsUtsGameSceneSetup — chạy Prepare scene trước.");
                return;
            }

            UnlockableSO[] catalog = LoadAllUnlockables();
            GameObject civilCentral = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.CivilCentralPrefabPath);
            GameObject workerMp = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.WorkerMpPrefabPath);

            var additional = new List<GameObject>();
            for (int i = 0; i < spawnCandidates.Count; i++)
            {
                GameObject prefab = spawnCandidates[i];
                if (prefab == null || prefab.GetComponent<NetworkIdentity>() == null)
                {
                    continue;
                }

                if (prefab == civilCentral || prefab == workerMp)
                {
                    continue;
                }

                additional.Add(prefab);
            }

            setup.unlockableCatalog = catalog;
            setup.additionalNetworkSpawnPrefabs = additional.ToArray();
            setup.civilCentralPrefab = civilCentral;
            setup.startingWorkerPrefab = workerMp;
            setup.startingWorkerCount = Mathf.Max(setup.startingWorkerCount, 3);

            EditorUtility.SetDirty(setup);
            Debug.Log(
                $"[RtsMpAutoSetup] RtsUtsGameSceneSetup: catalog={catalog.Length}, "
                + $"additionalSpawn={additional.Count}.");
        }

        static UnlockableSO[] LoadAllUnlockables()
        {
            string[] guids = AssetDatabase.FindAssets("t:UnlockableSO", new[] { "Assets/Data_Re" });
            var list = new List<UnlockableSO>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnlockableSO unlockable = AssetDatabase.LoadAssetAtPath<UnlockableSO>(path);
                if (unlockable != null)
                {
                    list.Add(unlockable);
                }
            }

            return list
                .OrderBy(u => u.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
#endif
