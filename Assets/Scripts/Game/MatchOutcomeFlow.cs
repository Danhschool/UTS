using GameDevTV.RTS.Game.Pregame;
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
        public const string DefaultDefeatScene = "MainMenu";

        /// <summary>
        /// Mục tiêu: Kết thúc trận với kết quả thua (đầu hàng).
        /// Cách hoạt động: Resume timeScale, load scene defeat (mặc định MainMenu nếu chưa có scene riêng).
        /// </summary>
        public static void LoadDefeatScene(string sceneName = null)
        {
            GamePauseService.ForceResumeForSceneChange();

            string target = string.IsNullOrWhiteSpace(sceneName) ? DefaultDefeatScene : sceneName.Trim();
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError($"[MatchOutcomeFlow] Scene '{target}' chưa có trong Build Settings.");
                return;
            }

            if (GameplayStartupScenes.IsLoadingScene(SceneManager.GetActiveScene()))
            {
                SceneManager.LoadScene(target);
                return;
            }

            GameplaySceneLoader.RequestLoad(target);
        }
    }
}
