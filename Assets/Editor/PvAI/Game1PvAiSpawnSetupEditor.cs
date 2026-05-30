#if UNITY_EDITOR
using GameDevTV.RTS.PvAI;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.PvAI
{
    /// <summary>
    /// Mục tiêu: Wire Game 1 cho spawn PvE long-term (không dùng CC cắm sẵn trong scene).
    /// </summary>
    public static class Game1PvAiSpawnSetupEditor
    {
        const string Game1ScenePath = "Assets/Scenes/Game 1.unity";
        const string CivilCentralPrefabPath = "Assets/Prefab/Buildings/civil_central/civil_central.prefab";
        const string WorkerPrefabPath = "Assets/Prefab/Unit/Worker.prefab";

        static readonly Vector3 DefaultHumanSpawn = new(-180f, 0f, -211f);
        static readonly Vector3 DefaultAiSpawn = new(191f, 0f, 168.4f);

        [MenuItem("ProjectRTS/PvAI/Setup Game 1 spawn (long-term PvE)")]
        public static void SetupGame1Spawn()
        {
            Scene scene = EditorSceneManager.OpenScene(Game1ScenePath, OpenSceneMode.Single);

            int removed = RemoveScenePlacedCivilCentrals();
            PvAiGameSceneSetup setup = EnsureSetupRoot();
            EnsureSpawnPoints(setup);
            WirePrefabs(setup);
            EnsureBootstrap(setup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[Game1PvAiSpawnSetup] Xong. Đã xóa {removed} civil_central cắm sẵn; spawn PvE qua PvAiGameSceneBootstrap khi Play.");
        }

        static PvAiGameSceneSetup EnsureSetupRoot()
        {
            PvAiGameSceneSetup setup = Object.FindFirstObjectByType<PvAiGameSceneSetup>(FindObjectsInactive.Include);
            if (setup != null)
            {
                setup.gameObject.name = "PvAiGameSceneSetup";
                return setup;
            }

            GameObject root = new GameObject("PvAiGameSceneSetup");
            setup = root.AddComponent<PvAiGameSceneSetup>();
            Undo.RegisterCreatedObjectUndo(root, "Create PvAiGameSceneSetup");
            return setup;
        }

        static void EnsureSpawnPoints(PvAiGameSceneSetup setup)
        {
            Transform human = setup.factionSpawnPoints != null && setup.factionSpawnPoints.Length > 0
                ? setup.factionSpawnPoints[0]
                : null;
            Transform ai = setup.factionSpawnPoints != null && setup.factionSpawnPoints.Length > 1
                ? setup.factionSpawnPoints[1]
                : null;

            if (human == null)
            {
                human = CreateSpawnPoint(setup.transform, "PvAiSpawn_Human", DefaultHumanSpawn);
            }

            if (ai == null)
            {
                ai = CreateSpawnPoint(setup.transform, "PvAiSpawn_AI", DefaultAiSpawn);
            }

            setup.factionSpawnPoints = new[] { human, ai };
            EditorUtility.SetDirty(setup);
        }

        static Transform CreateSpawnPoint(Transform parent, string name, Vector3 worldPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(go, "Create PvAI spawn point");
            return go.transform;
        }

        static void WirePrefabs(PvAiGameSceneSetup setup)
        {
            GameObject workerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPrefabPath);

            setup.civilCentralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilCentralPrefabPath);
            setup.humanOwner = Owner.Player1;
            setup.aiOwner = Owner.AI2;
            setup.spawnStartingUnits = true;
            setup.startingUnits = new[]
            {
                new StartingUnitSpawnEntry { unitPrefab = workerPrefab, count = 3 }
            };
            setup.unitSpawnFirstOffsetLocal = Vector3.zero;
            setup.unitSpawnSphereRadius = 3f;
            setup.destroyScenePlacedCivilCentrals = true;
            EditorUtility.SetDirty(setup);
        }

        static void EnsureBootstrap(PvAiGameSceneSetup setup)
        {
            PvAiGameSceneBootstrap bootstrap = setup.GetComponent<PvAiGameSceneBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = setup.gameObject.AddComponent<PvAiGameSceneBootstrap>();
            }

            SerializedObject so = new SerializedObject(bootstrap);
            so.FindProperty("sceneSetup").objectReferenceValue = setup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int RemoveScenePlacedCivilCentrals()
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

                Undo.DestroyObjectImmediate(building.gameObject);
                count++;
            }

            return count;
        }
    }
}
#endif
