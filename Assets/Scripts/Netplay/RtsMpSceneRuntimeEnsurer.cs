using GameDevTV.RTS.AI;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.PvAI;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Tự wire scene MP lúc Play — không cần thêm component thủ công sau auto-setup Editor.
    /// </summary>
    public static class RtsMpSceneRuntimeEnsurer
    {
        public static void EnsureGameplaySceneReady()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            DisableDuplicateBootstraps();
            DisableOfflineSystemsDuringNetplay();
            EnsurePresentationDirectorWired();
        }

        static void DisableDuplicateBootstraps()
        {
            RtsNetGameSceneBootstrap[] bootstraps = Object.FindObjectsByType<RtsNetGameSceneBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (bootstraps.Length <= 1)
            {
                return;
            }

            RtsNetGameSceneBootstrap keep = null;
            for (int i = 0; i < bootstraps.Length; i++)
            {
                RtsNetGameSceneBootstrap candidate = bootstraps[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.GetComponent<RtsUtsGameSceneSetup>() != null)
                {
                    keep = candidate;
                    break;
                }
            }

            keep ??= bootstraps[0];

            for (int i = 0; i < bootstraps.Length; i++)
            {
                RtsNetGameSceneBootstrap bootstrap = bootstraps[i];
                if (bootstrap != null && bootstrap != keep)
                {
                    bootstrap.enabled = false;
                }
            }
        }

        static void DisableOfflineSystemsDuringNetplay()
        {
            if (!RtsNetplaySession.IsNetworkMatch)
            {
                return;
            }

            PvAiGameSceneBootstrap[] pvBootstraps = Object.FindObjectsByType<PvAiGameSceneBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < pvBootstraps.Length; i++)
            {
                if (pvBootstraps[i] != null)
                {
                    pvBootstraps[i].enabled = false;
                }
            }

            AIController[] aiControllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < aiControllers.Length; i++)
            {
                AIController controller = aiControllers[i];
                if (controller != null && !HumanFogVisionUtility.IsHumanPlayer(controller.AiOwner))
                {
                    controller.enabled = false;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: MpPlayerPresentationDirector luôn có rig P1/P2 + HUD khi vào RtsNet_Game.
        /// Cách hoạt động: Tìm director hoặc tạo mới; auto-resolve reference từ scene.
        /// </summary>
        static void EnsurePresentationDirectorWired()
        {
            MpPlayerPresentationDirector director = Object.FindFirstObjectByType<MpPlayerPresentationDirector>(
                FindObjectsInactive.Include);

            if (director == null)
            {
                GameObject host = new GameObject("MpPlayerPresentation");
                director = host.AddComponent<MpPlayerPresentationDirector>();
            }

            director.TryAutoWireMissingReferences();
        }
    }
}
