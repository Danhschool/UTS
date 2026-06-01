#if UNITY_EDITOR
using GameDevTV.RTS.AI;
using GameDevTV.RTS.PvAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Copy AIGameSessionConfig vào Resources + gắn PregameAiDifficultyApplicator trên Game 1.
    /// </summary>
    public static class PregameGameplayDifficultySetupEditor
    {
        const string GameScenePath = "Assets/Scenes/Game 1.unity";
        const string SessionConfigPath = "Assets/Data_Re/AI/AIGameSessionConfig.asset";
        const string ResourcesDir = "Assets/Resources";
        const string ResourcesConfigPath = "Assets/Resources/AIGameSessionConfig.asset";

        [InitializeOnLoadMethod]
        static void ScheduleResourcesCheck()
        {
            EditorApplication.delayCall += EnsureResourcesConfigCopy;
        }

        [MenuItem("ProjectRTS/Pregame/Setup Game 1 AI Difficulty")]
        public static void SetupGame1AiDifficulty()
        {
            EnsureResourcesConfigCopy();

            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("Game 1.unity"))
            {
                EditorSceneManager.OpenScene(GameScenePath);
            }

            PvAiGameSceneBootstrap bootstrap = Object.FindFirstObjectByType<PvAiGameSceneBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                Debug.LogError("[PregameGameplayDifficultySetup] Game 1 không có PvAiGameSceneBootstrap.");
                return;
            }

            PregameAiDifficultyApplicator applicator = bootstrap.GetComponent<PregameAiDifficultyApplicator>();
            if (applicator == null)
            {
                applicator = bootstrap.gameObject.AddComponent<PregameAiDifficultyApplicator>();
            }

            SerializedObject serialized = new SerializedObject(applicator);
            serialized.FindProperty("logWhenApplied").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(applicator);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "[PregameGameplayDifficultySetup] Đã copy AIGameSessionConfig → Resources và gắn PregameAiDifficultyApplicator.");
        }

        static void EnsureResourcesConfigCopy()
        {
            AIGameSessionConfigSO source = AssetDatabase.LoadAssetAtPath<AIGameSessionConfigSO>(SessionConfigPath);
            if (source == null)
            {
                Debug.LogError($"[PregameGameplayDifficultySetup] Không tìm thấy {SessionConfigPath}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesDir))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AIGameSessionConfigSO existing = AssetDatabase.LoadAssetAtPath<AIGameSessionConfigSO>(ResourcesConfigPath);
            if (existing == null)
            {
                if (!AssetDatabase.CopyAsset(SessionConfigPath, ResourcesConfigPath))
                {
                    Debug.LogError("[PregameGameplayDifficultySetup] Copy asset vào Resources thất bại.");
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            EditorUtility.SetDirty(existing);
        }
    }
}
#endif
