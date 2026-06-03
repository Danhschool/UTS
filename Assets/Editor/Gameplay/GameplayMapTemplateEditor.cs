#if UNITY_EDITOR
using GameDevTV.RTS.Editor.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Gameplay
{
    /// <summary>
    /// SRP: Menu Editor tạo prefab lõi map và chuẩn bị scene gameplay đang mở.
    /// </summary>
    public static class GameplayMapTemplateEditor
    {
        const string ReferenceScenePath = GameplayMapScenePaths.ReferenceStableScenePath;

        [MenuItem("ProjectRTS/Gameplay/★ Create Or Update GameplayMapCore Prefab")]
        public static void CreateCorePrefabMenu()
        {
            GameplayMapSceneSetupApplicator.CreateOrUpdateCorePrefab();
            AssetDatabase.Refresh();
        }

        [MenuItem("ProjectRTS/Gameplay/Apply Gameplay Map Core To Open Scene")]
        public static void ApplyCoreToOpenSceneMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[GameplayMap] Không có scene đang mở.");
                return;
            }

            GameplayMapSceneSetupApplicator.ApplyCoreToOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[GameplayMap] Đã áp GameplayMapCore lên scene: {scene.name}");
        }

        [MenuItem("ProjectRTS/Gameplay/★ Prepare Open Scene As Gameplay Map (PvE + PvP)")]
        public static void PrepareOpenSceneMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[GameplayMap] Không có scene đang mở.");
                return;
            }

            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning(
                    "[GameplayMap] Scene chưa lưu. Save As (ví dụ Assets/Scenes/MyMap.unity) rồi chạy lại menu.");
            }

            GameplayMapSceneSetupApplicator.PrepareOpenGameplayMapScene(wirePresentation: true);
            GameplayMapSceneSetupApplicator.TryAddActiveSceneToBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[GameplayMap] Xong '{scene.name}'. Chỉ cần vẽ terrain, đặt supply & animal; kéo Team0/Team1 spawn nếu cần. "
                + "Thêm entry trong Pregame map catalog (SSScene) để chọn từ menu.");
        }

        [MenuItem("ProjectRTS/Gameplay/Ensure MP Presentation On Open Scene")]
        public static void EnsureMpPresentationMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[GameplayMap] Không có scene đang mở.");
                return;
            }

            MpPresentationSceneSetupEditor.WireInOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[GameplayMap] Đã ensure MP presentation (fog x2 + HUD x2) trên {scene.name}. Save scene.");
        }

        [MenuItem("ProjectRTS/Gameplay/Add Open Scene To Build Settings")]
        public static void AddBuildSettingsMenu()
        {
            GameplayMapSceneSetupApplicator.TryAddActiveSceneToBuildSettings();
        }

        [MenuItem("ProjectRTS/Gameplay/Sync From Stable Reference (RtsNet_Game)")]
        public static void SyncFromReferenceSceneMenu()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene reference = EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Single);
            GameplayMapSceneSetupApplicator.PrepareOpenGameplayMapScene(wirePresentation: true);
            GameplayMapSceneSetupApplicator.CreateOrUpdateCorePrefab();
            EditorSceneManager.MarkSceneDirty(reference);
            EditorSceneManager.SaveScene(reference);

            if (previous.IsValid() && !string.IsNullOrEmpty(previous.path))
            {
                EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
            }

            AssetDatabase.Refresh();
            Debug.Log(
                "[GameplayMap] Đã cập nhật RtsNet_Game + prefab GameplayMapCore. Dùng Prepare trên map mới sau khi vẽ bản đồ.");
        }
    }
}
#endif
