using GameDevTV.RTS.AI;
using GameDevTV.RTS.Game.Pregame;
using GameDevTV.RTS.Game.Startup;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Áp <see cref="PregameSessionState.SelectedDifficulty"/> lên mọi <see cref="AIController"/> trong scene game offline.
    /// </summary>
    public static class PregameAiDifficultyApplyService
    {
        const string ResourcesConfigPath = "AIGameSessionConfig";
        const string EditorConfigAssetPath = "Assets/Data_Re/AI/AIGameSessionConfig.asset";

        static bool s_appliedThisSession;
        static int s_appliedLoadGeneration = -1;
        static AIGameSessionConfigSO cachedConfig;

        /// <summary>
        /// Mục tiêu: Gọi sau khi vào Game 1 từ SSScene (hoặc Play trực tiếp scene).
        /// Cách hoạt động: Resolve config → quét AIController → SetDifficulty; log kết quả.
        /// </summary>
        public static bool TryApply(bool logResult = true)
        {
            if (!ShouldApplyInActiveScene())
            {
                if (logResult)
                {
                    Debug.Log("[PregameAI] Bỏ qua áp độ khó — không phải Game offline hoặc Mirror đang active.");
                }

                return false;
            }

            if (s_appliedThisSession && s_appliedLoadGeneration == PvAiOfflineSessionPrep.CurrentLoadGeneration)
            {
                return true;
            }

            PvAiOfflineAiCoordinator.TryEnableForOfflinePvE();

            AIGameSessionConfigSO config = ResolveSessionConfig();
            if (config == null)
            {
                Debug.LogWarning(
                    "[PregameAI] Không load được AIGameSessionConfig. "
                    + "Chạy ProjectRTS/Pregame/Setup Game 1 AI Difficulty (copy vào Resources).");
                return false;
            }

            AIController[] controllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (controllers.Length == 0)
            {
                if (logResult)
                {
                    Debug.LogWarning("[PregameAI] Không tìm thấy AIController trong scene — chưa áp độ khó.");
                }

                return false;
            }

            AIDifficultyLevel level = PregameSessionState.SelectedDifficulty;
            AIDifficultySO profile = config.Resolve(level);
            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i].SetDifficulty(config, level);
            }

            s_appliedThisSession = true;
            s_appliedLoadGeneration = PvAiOfflineSessionPrep.CurrentLoadGeneration;

            if (logResult)
            {
                string profileName = profile != null ? profile.name : "null";
                Debug.Log(
                    $"[PregameAI] Đã áp độ khó {level} ({profileName}) cho {controllers.Length} AIController. "
                    + $"Nguồn: PregameSessionState sau SSScene.");
            }

            return true;
        }

        static bool ShouldApplyInActiveScene()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            return GameplayStartupScenes.IsGameplayScene(scene);
        }

        static AIGameSessionConfigSO ResolveSessionConfig()
        {
            if (cachedConfig != null)
            {
                return cachedConfig;
            }

            cachedConfig = Resources.Load<AIGameSessionConfigSO>(ResourcesConfigPath);
#if UNITY_EDITOR
            if (cachedConfig == null)
            {
                cachedConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<AIGameSessionConfigSO>(EditorConfigAssetPath);
            }
#endif
            return cachedConfig;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_appliedThisSession = false;
            s_appliedLoadGeneration = -1;
            cachedConfig = null;
        }
    }
}
