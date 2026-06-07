using GameDevTV.RTS.Audio;
using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.PvAI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: API chuyển scene gameplay qua Loading scene (Phase A — load file / chờ MP).
    /// </summary>
    public static class GameplaySceneLoader
    {
        public const string LoadingSceneName = "Loading";

        const float PhaseAProgressMax = 0.85f;

        static string pendingTargetScene;
        static bool pendingUseNetworkHandoff;
        static bool enteredGameplayFromLoader;
        static bool loadFlowActive;

        public static float PhaseAProgressCap => PhaseAProgressMax;
        public static bool HasPendingTarget => !string.IsNullOrWhiteSpace(pendingTargetScene);
        public static string PendingTargetScene => pendingTargetScene;
        public static bool PendingUseNetworkHandoff => pendingUseNetworkHandoff;
        public static bool EnteredGameplayFromLoader => enteredGameplayFromLoader;
        public static bool IsLoadFlowActive => loadFlowActive;

        /// <summary>
        /// Mục tiêu: Offline / menu → Loading → game (async LoadSceneAsync).
        /// Cách hoạt động: Lưu scene đích rồi LoadScene(Loading).
        /// </summary>
        public static void RequestLoad(string targetScene, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogError("[GameplaySceneLoader] targetScene rỗng.");
                return;
            }

            pendingTargetScene = targetScene;
            pendingUseNetworkHandoff = false;
            enteredGameplayFromLoader = false;
            loadFlowActive = true;
            FactionSummaryTracker.ResetForNewGameplay();
            PvAiOfflineSessionPrep.OnGameplayLoadRequested();
            PregameAudioTransition.StopMenuMusicForGameplay();

            if (SceneManager.GetActiveScene().name == LoadingSceneName)
            {
                GameplayLoadingSceneController.StartPendingLoadOnActiveScene();
                return;
            }

            SceneManager.LoadScene(LoadingSceneName);
        }

        /// <summary>
        /// Mục tiêu: Mirror host chuyển qua Loading trước scene game.
        /// </summary>
        public static void BeginNetworkGameplayLoad(string targetScene)
        {
            pendingTargetScene = targetScene;
            pendingUseNetworkHandoff = true;
            enteredGameplayFromLoader = false;
            loadFlowActive = true;
            PregameAudioTransition.StopMenuMusicForGameplay();
        }

        public static void MarkGameplayEntered()
        {
            enteredGameplayFromLoader = true;
            loadFlowActive = true;
            ClearPending();
        }

        public static void CompleteLoadFlow()
        {
            loadFlowActive = false;
        }

        public static void ClearPending()
        {
            pendingTargetScene = null;
            pendingUseNetworkHandoff = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RedirectDirectGameplayScenePlay()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.name == LoadingSceneName)
            {
                return;
            }

            if (!GameplayStartupScenes.IsGameplayScene(active))
            {
                return;
            }

            if (enteredGameplayFromLoader || loadFlowActive || HasPendingTarget)
            {
                return;
            }

            if (Mirror.NetworkClient.active)
            {
                return;
            }

            RequestLoad(active.name);
        }
    }
}
