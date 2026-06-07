using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Game.Startup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Chuyển scene khi trận kết thúc (đầu hàng / thua).
    /// </summary>
    public static class MatchOutcomeFlow
    {
        public const string DefaultEndScene = "End";
        public const string DefaultMenuSceneName = "MainMenu";

        static string activeEndSceneName = DefaultEndScene;

        public static string ActiveEndSceneName => activeEndSceneName;

        /// <summary>
        /// Mục tiêu: HUD gameplay gán scene End (Inspector) cho đầu hàng / kết thúc trận.
        /// Cách hoạt động: Lưu tên scene hợp lệ; LoadEndScene dùng giá trị này khi không truyền override.
        /// </summary>
        public static void SetActiveEndScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            activeEndSceneName = sceneName.Trim();
        }

        /// <summary>
        /// Mục tiêu: Chuyển sang scene End sau khi trận kết thúc.
        /// Cách hoạt động: Resume timeScale rồi load trực tiếp End (không qua Loading).
        /// </summary>
        public static void LoadEndScene(string sceneName = null)
        {
            LoadSceneInternal(sceneName, activeEndSceneName);
        }

        /// <summary>
        /// Mục tiêu: Quay về MainMenu từ scene End hoặc fallback.
        /// Cách hoạt động: Resume timeScale rồi load trực tiếp menu.
        /// </summary>
        public static void LoadMenuScene(string sceneName = null)
        {
            FactionSummaryTracker.DestroyRuntimeInstance();
            LoadSceneInternal(sceneName, DefaultMenuSceneName);
        }

        static void LoadSceneInternal(string sceneName, string defaultScene)
        {
            GamePauseService.ForceResumeForSceneChange();

            string target = string.IsNullOrWhiteSpace(sceneName) ? defaultScene : sceneName.Trim();
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError($"[MatchOutcomeFlow] Scene '{target}' chưa có trong Build Settings.");
                return;
            }

            GameplaySceneLoader.CompleteLoadFlow();
            GameplaySceneLoader.ClearPending();
            SceneManager.LoadScene(target);
        }
    }
}
