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
            DisableConflictingPresentationBinders();
            EnsurePresentationDirectorWired();
        }

        /// <summary>
        /// Mục tiêu: PlayerViewBinder (offline) ghi đè fog/HUD khi MP Director đã có rig — dù scene "đủ" component.
        /// Cách hoạt động: Tắt binder lúc Play nếu network match + director có rig.
        /// </summary>
        static void DisableConflictingPresentationBinders()
        {
            if (!RtsNetplaySession.IsNetworkMatch)
            {
                return;
            }

            MpPlayerPresentationDirector director = Object.FindFirstObjectByType<MpPlayerPresentationDirector>(
                FindObjectsInactive.Include);
            if (director == null || !director.HasConfiguredRigs)
            {
                return;
            }

            PlayerViewBinder[] binders = Object.FindObjectsByType<PlayerViewBinder>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < binders.Length; i++)
            {
                PlayerViewBinder binder = binders[i];
                if (binder != null && binder.enabled)
                {
                    binder.enabled = false;
                }
            }
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
        /// Mục tiêu: MpPlayerPresentationDirector luôn có rig P1/P2 + HUD khi vào Game 1 / Game 2 MP.
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

            MpPlayerPresentationRig[] rigs = Object.FindObjectsByType<MpPlayerPresentationRig>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < rigs.Length; i++)
            {
                rigs[i]?.TryResolveHudReferencesFromScene();
            }
        }
    }
}
