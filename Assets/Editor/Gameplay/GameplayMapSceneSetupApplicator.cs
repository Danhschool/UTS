#if UNITY_EDITOR
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Gameplay;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.PvAI;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Editor.Netplay;
using Mirror;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Gameplay
{
    /// <summary>
    /// SRP: Áp cấu hình lõi map (spawn PvE + PvP) lên scene gameplay đang mở — không hardcode một scene.
    /// </summary>
    public static class GameplayMapSceneSetupApplicator
    {
        static readonly Vector3 DefaultTeam0Local = new(-185f, 0f, -174f);
        static readonly Vector3 DefaultTeam1Local = new(185f, 0f, 174f);

        /// <summary>
        /// Mục tiêu: Chuẩn bị scene đang mở cho PvE + PvP (core + presentation + dọn prefab network trong scene).
        /// Cách hoạt động: Gọi lần lượt ApplyCore, wire MP presentation, tắt offline bootstrap, sync tên bootstrap.
        /// </summary>
        public static void PrepareOpenGameplayMapScene(bool wirePresentation = true)
        {
            ApplyCoreToOpenScene();
            if (wirePresentation)
            {
                MpPresentationSceneSetupEditor.WireInOpenScene();
            }

            EnsureAiForOfflineGameplay();
            EnsureOfflineOwnerBootstrap();
            RemoveScenePlacedNetworkPrefabsForActiveScene();
            RemoveScenePlacedCivilCentrals();
            EnsureUtsPrefabNetworkComponents();
            EnsurePvAiDifficultyApplicator();
        }

        /// <summary>
        /// Mục tiêu: Tạo hoặc cập nhật prefab GameplayMapCore từ logic chuẩn (spawn + setup components).
        /// Cách hoạt động: Build hierarchy tạm, SaveAsPrefabAsset, hủy bản tạm.
        /// </summary>
        public static void CreateOrUpdateCorePrefab()
        {
            EnsurePrefabFolderExists();

            GameObject tempRoot = new GameObject(GameplayMapScenePaths.CoreRootName);
            try
            {
                BuildCoreHierarchy(tempRoot);
                PrefabUtility.SaveAsPrefabAsset(tempRoot, GameplayMapScenePaths.CorePrefabPath);
                Debug.Log($"[GameplayMap] Đã lưu prefab: {GameplayMapScenePaths.CorePrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(tempRoot);
            }
        }

        /// <summary>
        /// Mục tiêu: Scene map mới có đủ GameplayMapCore (từ prefab hoặc tạo tay) với spawn + PvE/PvP setup.
        /// Cách hoạt động: Instantiate prefab nếu thiếu; nếu có root cũ thì cập nhật component in-place.
        /// </summary>
        public static void ApplyCoreToOpenScene()
        {
            GameObject coreRoot = FindOrCreateCoreRoot();
            BuildCoreHierarchy(coreRoot);
            EditorUtility.SetDirty(coreRoot);
        }

        /// <summary>
        /// Mục tiêu: Scene gameplay mới có trong Build Settings để LoadScene / Mirror hoạt động.
        /// Cách hoạt động: Thêm path scene active nếu chưa có trong EditorBuildSettings.
        /// </summary>
        public static bool TryAddActiveSceneToBuildSettings()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || string.IsNullOrEmpty(active.path))
            {
                Debug.LogWarning("[GameplayMap] Scene chưa lưu — Save Scene trước khi thêm Build Settings.");
                return false;
            }

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == active.path)
                {
                    Debug.Log($"[GameplayMap] Scene đã có trong Build Settings: {active.name}");
                    return true;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(active.path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[GameplayMap] Đã thêm Build Settings: {active.path}");
            return true;
        }

        static void BuildCoreHierarchy(GameObject coreRoot)
        {
            if (coreRoot.GetComponent<GameplayMapCoreMarker>() == null)
            {
                coreRoot.AddComponent<GameplayMapCoreMarker>();
            }

            coreRoot.name = GameplayMapScenePaths.CoreRootName;

            Transform team0 = FindOrCreateChildSpawn(coreRoot.transform, "Team0Spawn", DefaultTeam0Local);
            Transform team1 = FindOrCreateChildSpawn(coreRoot.transform, "Team1Spawn", DefaultTeam1Local);

            WireMpSpawnSetup(coreRoot, team0, team1);
            WirePvAiSpawnSetup(coreRoot, team0, team1);
            EnsureMpBootstrapComponents(coreRoot);
        }

        static GameObject FindOrCreateCoreRoot()
        {
            GameplayMapCoreMarker marker = Object.FindFirstObjectByType<GameplayMapCoreMarker>(
                FindObjectsInactive.Include);
            if (marker != null)
            {
                return marker.gameObject;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayMapScenePaths.CorePrefabPath);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    prefab,
                    SceneManager.GetActiveScene());
                instance.name = GameplayMapScenePaths.CoreRootName;
                return instance;
            }

            RtsUtsGameSceneSetup legacyUts = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(
                FindObjectsInactive.Include);
            if (legacyUts != null)
            {
                return legacyUts.gameObject;
            }

            PvAiGameSceneSetup legacyPv = Object.FindFirstObjectByType<PvAiGameSceneSetup>(
                FindObjectsInactive.Include);
            if (legacyPv != null)
            {
                return legacyPv.gameObject;
            }

            return new GameObject(GameplayMapScenePaths.CoreRootName);
        }

        static Transform FindOrCreateChildSpawn(Transform parent, string name, Vector3 localPosition)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            GameObject sceneObject = GameObject.Find(name);
            if (sceneObject != null && sceneObject.scene == parent.gameObject.scene)
            {
                sceneObject.transform.SetParent(parent);
                return sceneObject.transform;
            }

            GameObject spawn = new GameObject(name);
            spawn.transform.SetParent(parent);
            spawn.transform.localPosition = localPosition;
            spawn.transform.localRotation = Quaternion.identity;
            return spawn.transform;
        }

        static void WireMpSpawnSetup(GameObject coreRoot, Transform team0, Transform team1)
        {
            RtsUtsGameSceneSetup utsSetup = coreRoot.GetComponent<RtsUtsGameSceneSetup>();
            if (utsSetup == null)
            {
                utsSetup = coreRoot.AddComponent<RtsUtsGameSceneSetup>();
            }

            utsSetup.teamSpawnPoints = new[] { team0, team1 };
            utsSetup.civilCentralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.CivilCentralPrefabPath);
            utsSetup.startingWorkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.WorkerMpPrefabPath);
            utsSetup.startingWorkerCount = Mathf.Max(utsSetup.startingWorkerCount, 3);
            utsSetup.workerOffsetFromBase = new Vector3(10f, 0f, 10f);
            utsSetup.workerSpawnSpacing = new Vector3(10f, 0f, 0f);
            utsSetup.disableAiControllersOnLoad = true;
            EditorUtility.SetDirty(utsSetup);

            RtsGameSceneSetup legacySetup = coreRoot.GetComponent<RtsGameSceneSetup>();
            if (legacySetup == null)
            {
                legacySetup = coreRoot.AddComponent<RtsGameSceneSetup>();
            }

            legacySetup.teamSpawnPoints = new[] { team0, team1 };
            EditorUtility.SetDirty(legacySetup);
        }

        static void WirePvAiSpawnSetup(GameObject coreRoot, Transform team0, Transform team1)
        {
            PvAiGameSceneSetup pvSetup = coreRoot.GetComponent<PvAiGameSceneSetup>();
            if (pvSetup == null)
            {
                pvSetup = coreRoot.AddComponent<PvAiGameSceneSetup>();
            }

            GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.WorkerPvAiPrefabPath);

            pvSetup.factionSpawnPoints = new[] { team0, team1 };
            pvSetup.civilCentralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.CivilCentralPrefabPath);
            pvSetup.humanOwner = Owner.Player1;
            pvSetup.aiOwner = Owner.AI2;
            pvSetup.spawnStartingUnits = true;
            pvSetup.startingUnits = new[]
            {
                new StartingUnitSpawnEntry { unitPrefab = workerPrefab, count = 3 },
            };
            pvSetup.unitSpawnFirstOffsetLocal = Vector3.zero;
            pvSetup.unitSpawnSphereRadius = 3f;
            pvSetup.destroyScenePlacedCivilCentrals = true;
            EditorUtility.SetDirty(pvSetup);

            PvAiGameSceneBootstrap pvBootstrap = coreRoot.GetComponent<PvAiGameSceneBootstrap>();
            if (pvBootstrap == null)
            {
                pvBootstrap = coreRoot.AddComponent<PvAiGameSceneBootstrap>();
            }

            SerializedObject pvSo = new SerializedObject(pvBootstrap);
            pvSo.FindProperty("sceneSetup").objectReferenceValue = pvSetup;
            pvSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pvBootstrap);
        }

        static void EnsureMpBootstrapComponents(GameObject coreRoot)
        {
            if (coreRoot.GetComponent<RtsNetGameSceneBootstrap>() == null)
            {
                coreRoot.AddComponent<RtsNetGameSceneBootstrap>();
            }

            if (coreRoot.GetComponent<RtsMatchServerSpawnRunner>() == null)
            {
                coreRoot.AddComponent<RtsMatchServerSpawnRunner>();
            }

            if (coreRoot.GetComponent<NetworkIdentity>() == null)
            {
                coreRoot.AddComponent<NetworkIdentity>();
            }

            if (coreRoot.GetComponent<GameMatchOverlayStateSync>() == null)
            {
                coreRoot.AddComponent<GameMatchOverlayStateSync>();
            }
        }

        static void EnsurePrefabFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefab");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefab/Gameplay"))
            {
                AssetDatabase.CreateFolder("Assets/Prefab", "Gameplay");
            }
        }

        static void EnsureOfflineOwnerBootstrap()
        {
            LocalHumanOwnerBootstrap[] bootstraps = Object.FindObjectsByType<LocalHumanOwnerBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bootstraps.Length; i++)
            {
                if (bootstraps[i] != null)
                {
                    bootstraps[i].enabled = true;
                }
            }
        }

        static void EnsureAiForOfflineGameplay()
        {
            AIController[] controllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                AIController controller = controllers[i];
                if (controller == null || HumanFogVisionUtility.IsHumanPlayer(controller.AiOwner))
                {
                    continue;
                }

                controller.enabled = true;
            }
        }

        static void EnsureUtsPrefabNetworkComponents()
        {
            EnsureNetworkEntityOnPrefab(GameplayMapScenePaths.CivilCentralPrefabPath);
            EnsureNetworkEntityOnPrefab(GameplayMapScenePaths.WorkerMpPrefabPath);
        }

        static void EnsureNetworkEntityOnPrefab(string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[GameplayMap] Không tìm thấy prefab: {assetPath}");
                return;
            }

            if (prefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogWarning($"[GameplayMap] {prefab.name} thiếu NetworkIdentity.");
                return;
            }

            if (prefab.GetComponent<RtsUtsNetworkEntity>() == null)
            {
                prefab.AddComponent<RtsUtsNetworkEntity>();
                EditorUtility.SetDirty(prefab);
                Debug.Log($"[GameplayMap] Đã thêm RtsUtsNetworkEntity → {prefab.name}");
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh Mirror sceneId trùng trên CC/Worker đặt sẵn trong scene.
        /// Cách hoạt động: Xóa instance trong scene active khớp prefab MP spawn.
        /// </summary>
        public static void RemoveScenePlacedNetworkPrefabsForActiveScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                return;
            }

            var paths = new System.Collections.Generic.HashSet<string>
            {
                GameplayMapScenePaths.CivilCentralPrefabPath,
                GameplayMapScenePaths.WorkerMpPrefabPath,
            };

            NetworkIdentity[] identities = Object.FindObjectsByType<NetworkIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < identities.Length; i++)
            {
                NetworkIdentity identity = identities[i];
                if (identity == null || identity.gameObject.scene != active)
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

                Object.DestroyImmediate(identity.transform.root.gameObject);
            }
        }

        public static int RemoveScenePlacedCivilCentrals()
        {
            int count = 0;
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = buildings.Length - 1; i >= 0; i--)
            {
                BaseBuilding building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (!building.gameObject.name.ToLowerInvariant().Contains("civil_central"))
                {
                    continue;
                }

                Object.DestroyImmediate(building.gameObject);
                count++;
            }

            return count;
        }

        public static void EnsurePvAiDifficultyApplicator()
        {
            PvAiGameSceneBootstrap bootstrap = Object.FindFirstObjectByType<PvAiGameSceneBootstrap>(
                FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                return;
            }

            if (bootstrap.GetComponent<PregameAiDifficultyApplicator>() == null)
            {
                bootstrap.gameObject.AddComponent<PregameAiDifficultyApplicator>();
                EditorUtility.SetDirty(bootstrap);
            }
        }
    }
}
#endif
