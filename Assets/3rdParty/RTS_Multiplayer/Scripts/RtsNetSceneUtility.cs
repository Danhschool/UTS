using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: So khớp scene Mirror ([Scene] lưu path) với scene đang active.
    /// </summary>
    public static class RtsNetSceneUtility
    {
        /// <summary>
        /// Mục tiêu: OnServerSceneChanged nhận diện RtsNet_Game dù gameScene là asset path.
        /// Cách hoạt động: Khớp scene.name, scene.path, hoặc tên file *.unity.
        /// </summary>
        public static bool MatchesActiveScene(string sceneField)
        {
            if (string.IsNullOrWhiteSpace(sceneField))
            {
                return false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.name == sceneField || active.path == sceneField)
            {
                return true;
            }

            if (sceneField.EndsWith(".unity"))
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(sceneField);
                return active.name == fileName;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Mirror [Scene] có thể lưu path — chuẩn hóa về tên scene trong Build Settings.
        /// Cách hoạt động: Lấy file name không .unity; giữ nguyên nếu đã là tên ngắn.
        /// </summary>
        public static string NormalizeSceneName(string sceneField)
        {
            if (string.IsNullOrWhiteSpace(sceneField))
            {
                return PregameGameplaySceneFallback.DefaultScene;
            }

            sceneField = sceneField.Trim();
            if (sceneField.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            {
                return System.IO.Path.GetFileNameWithoutExtension(sceneField);
            }

            return sceneField;
        }
    }
}
