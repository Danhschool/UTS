#if UNITY_EDITOR
using GameDevTV.RTS.AI;
using GameDevTV.RTS.UI.GameEventLog;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.AI.Editor
{
    /// <summary>
    /// SRP: Nhân bản scene chơi thành AI_Sandbox và gắn checklist win/lose Civil Central.
    /// </summary>
    public static class AISandboxSceneCreator
    {
        const string SourceScene = "Assets/Scenes/Game 1.unity";
        const string SandboxScene = "Assets/Scenes/AI_Sandbox.unity";
        const string MediumDifficulty = "Assets/Data_Re/AI/Difficulty/AIDifficulty_Medium.asset";

        [MenuItem("RTS/AI/Create AI Sandbox Scene")]
        public static void CreateSandboxScene()
        {
            AIDataReAssetCreator.CreateAll();

            if (!System.IO.File.Exists(SourceScene))
            {
                Debug.LogError($"[RTS AI] Source scene not found: {SourceScene}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScene) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "AI Sandbox",
                        $"{SandboxScene} already exists. Overwrite from Game 1?",
                        "Overwrite",
                        "Cancel"))
                {
                    return;
                }

                AssetDatabase.DeleteAsset(SandboxScene);
            }

            if (!AssetDatabase.CopyAsset(SourceScene, SandboxScene))
            {
                Debug.LogError("[RTS AI] Failed to copy scene to AI_Sandbox.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);
            WireSandboxComponents();
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[RTS AI] Created {SandboxScene}. Enable AIBot objects, assign difficulty SO, run Play. " +
                "See docs/AI_SANDBOX_CHECKLIST.md.");
        }

        static void WireSandboxComponents()
        {
            AIDifficultySO medium = AssetDatabase.LoadAssetAtPath<AIDifficultySO>(MediumDifficulty);

            AIController[] controllers = Object.FindObjectsByType<AIController>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                AIController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.gameObject.SetActive(true);
                SerializedObject so = new(controller);
                so.FindProperty("difficultyProfile").objectReferenceValue = medium;
                so.FindProperty("logTickSummary").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            EnsureWinLoseChecklist();
        }

        static void EnsureWinLoseChecklist()
        {
            AISandboxWinLoseChecklist existing =
                Object.FindFirstObjectByType<AISandboxWinLoseChecklist>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            PlayerGameEventLogListener listener =
                Object.FindFirstObjectByType<PlayerGameEventLogListener>(FindObjectsInactive.Include);
            GameObject host = listener != null ? listener.gameObject : new GameObject("AI_Sandbox_Checklist");
            if (listener == null)
            {
                Undo.RegisterCreatedObjectUndo(host, "Create AI Sandbox Checklist");
            }

            Undo.AddComponent<AISandboxWinLoseChecklist>(host);
        }
    }
}
#endif
