#if UNITY_EDITOR
using GameDevTV.RTS.Editor.Gameplay;
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
        [MenuItem("ProjectRTS/PvAI/Setup Game 1 spawn (long-term PvE)")]
        public static void SetupGame1Spawn()
        {
            Scene scene = EditorSceneManager.OpenScene(Game1ScenePath, OpenSceneMode.Single);

            int removed = GameplayMapSceneSetupApplicator.RemoveScenePlacedCivilCentrals();
            GameplayMapSceneSetupApplicator.ApplyCoreToOpenScene();
            GameplayMapSceneSetupApplicator.EnsurePvAiDifficultyApplicator();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[Game1PvAiSpawnSetup] Xong. Đã xóa {removed} civil_central cắm sẵn; dùng GameplayMapCore + PvAiGameSceneBootstrap.");
        }
    }
}
#endif
