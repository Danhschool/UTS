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
        /// Mục tiêu: So khớp scene Mirror (Game 1/2, path .unity, hoặc tên ngắn).
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

        /// <summary>
        /// Mục tiêu: Nhận diện map gameplay (Game 1/2, RtsNet_Game) mà không phụ thuộc Assembly-CSharp.
        /// Cách hoạt động: Loại lobby/menu/loading; chấp nhận tên Game* hoặc RtsNet_Game.
        /// </summary>
        public static bool IsActiveGameplayMapScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded)
            {
                return false;
            }

            string name = active.name;
            if (name == "RtsNet_Lobby"
                || name == "MainMenu"
                || name == "SSScene"
                || name == "Loading"
                || name == "End")
            {
                return false;
            }

            return name == "RtsNet_Game" || name.StartsWith("Game");
        }
    }
}
