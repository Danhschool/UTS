#if UNITY_EDITOR
using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.PvAI;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.PvAI
{
    /// <summary>
    /// Mục tiêu: M5 — sửa AIController aiOwner và prefab/scene Owner=2 (bot) → AI2.
    /// </summary>
    public static class Game1PvAiRegressionEditor
    {
        const string Game1ScenePath = "Assets/Scenes/Game 1.unity";

        [MenuItem("ProjectRTS/PvAI/Validate Game 1 (M5 checklist — không cần Play)")]
        public static void ValidateGame1Scene()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool openedGame1 = false;

            if (!previous.path.Contains("Game 1"))
            {
                EditorSceneManager.OpenScene(Game1ScenePath, OpenSceneMode.Single);
                openedGame1 = true;
            }

            IReadOnlyList<PvAiHealthFinding> findings = PvAiSceneValidator.Validate(playMode: false);
            string report = PvAiSceneValidator.FormatReport(findings);
            bool pass = PvAiSceneValidator.AllPassed(findings);

            if (pass)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }

            EditorUtility.DisplayDialog(
                pass ? "PvAI — OK" : "PvAI — Có lỗi",
                report,
                "Đóng");

            if (openedGame1 && !string.IsNullOrEmpty(previous.path))
            {
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }
        }

        [MenuItem("ProjectRTS/PvAI/Add Runtime Health Check to open scene")]
        public static void AddRuntimeHealthCheckToScene()
        {
            if (Object.FindFirstObjectByType<PvAiRuntimeHealthCheck>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[Game1PvAiRegression] PvAiRuntimeHealthCheck đã có trong scene.");
                return;
            }

            var go = new GameObject("PvAI_RuntimeHealthCheck");
            go.AddComponent<PvAiRuntimeHealthCheck>();
            Undo.RegisterCreatedObjectUndo(go, "Add PvAI Runtime Health Check");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Game1PvAiRegression] Đã thêm PvAiRuntimeHealthCheck — Play Game 1 để xem báo cáo + debug-559c4e.log.");
        }

        [MenuItem("ProjectRTS/PvAI/M5 Fix Game 1 AI owners + bot Owner=2 migration")]
        public static void FixGame1Regression()
        {
            int aiControllersFixed = FixAiControllersInOpenScene();
            int commandablesFixed = FixBotPlayer2OwnersInScene();

            if (!string.IsNullOrEmpty(Game1ScenePath))
            {
                Scene scene = EditorSceneManager.OpenScene(Game1ScenePath, OpenSceneMode.Single);
                aiControllersFixed += FixAiControllersInOpenScene();
                commandablesFixed += FixBotPlayer2OwnersInScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            int prefabsFixed = FixBotOwnersInPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Game1PvAiRegression] AIController fixed={aiControllersFixed}, scene Owner Player2→AI2={commandablesFixed}, prefabs={prefabsFixed}");
        }

        static int FixAiControllersInOpenScene()
        {
            int count = 0;
            AIController[] controllers = Object.FindObjectsByType<AIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                AIController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(controller);
                SerializedProperty aiOwner = so.FindProperty("aiOwner");
                Owner current = (Owner)aiOwner.intValue;
                if (current == Owner.Player1 || current == Owner.Player2)
                {
                    aiOwner.intValue = (int)Owner.AI2;
                    EditorUtility.SetDirty(controller);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }

            return count;
        }

        static int FixBotPlayer2OwnersInScene()
        {
            int count = 0;
            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < commandables.Length; i++)
            {
                if (TryMigrateBotPlayer2(commandables[i], isPrefab: false))
                {
                    count++;
                }
            }

            return count;
        }

        static int FixBotOwnersInPrefabs()
        {
            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                AbstractCommandable[] commandables = root.GetComponentsInChildren<AbstractCommandable>(true);
                for (int c = 0; c < commandables.Length; c++)
                {
                if (TryMigrateBotPlayer2(commandables[c], isPrefab: true))
                {
                    changed = true;
                    count++;
                }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            return count;
        }

        /// <summary>
        /// Mục tiêu: Prefab bot cũ dùng Owner=2 (trước đây AI1) chuyển sang AI2.
        /// Cách hoạt động: Chỉ đổi khi không phải human Player2 trong scene MP (heuristic: tên chứa AI/bot/enemy).
        /// </summary>
        static bool TryMigrateBotPlayer2(AbstractCommandable commandable, bool isPrefab)
        {
            if (commandable == null || commandable.Owner != Owner.Player2)
            {
                return false;
            }

            if (!isPrefab)
            {
                string name = commandable.gameObject.name.ToLowerInvariant();
                bool looksLikeBot = name.Contains("ai")
                                    || name.Contains("bot")
                                    || name.Contains("enemy")
                                    || name.Contains("opponent");
                if (!looksLikeBot)
                {
                    return false;
                }
            }

            SerializedObject so = new SerializedObject(commandable);
            SerializedProperty ownerProp = so.FindProperty("<Owner>k__BackingField");
            if (ownerProp == null)
            {
                return false;
            }

            ownerProp.intValue = (int)Owner.AI2;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(commandable);
            return true;
        }
    }
}
#endif
