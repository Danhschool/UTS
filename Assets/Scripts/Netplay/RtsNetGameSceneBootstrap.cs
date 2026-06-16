using System.Collections;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.UI.InGame;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Bootstrap map gameplay (Game 1/2) — sync LocalOwner, presentation MP, tắt capsule input.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RtsNetGameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] bool disableCapsuleGameInput = true;

        void Awake()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                RtsUtsServerEntityFactory.ResetMatchRegistration();
                return;
            }

            LocalHumanOwnerService.EnsureExists();
            FactionSummaryTracker.EnsureExists();
            MatchOutcomeDetector.EnsureExists();
            MatchOutcomeDetector.ResetForNewMatch();
            GameMatchOverlayStateSync.ResetMatchEndBroadcast();
            RtsLobbyUI.HideLobbyCanvasForGameplay();
            RtsMpSceneRuntimeEnsurer.EnsureGameplaySceneReady();
        }

        void Start()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            StartCoroutine(RefreshAfterNetworkReady());
            StartCoroutine(WarnIfPlayedWithoutLobbySession());
        }

        /// <summary>
        /// Mục tiêu: Cảnh báo khi Play map trực tiếp ở chế độ MP mà chưa qua Lobby Host/Client.
        /// </summary>
        IEnumerator WarnIfPlayedWithoutLobbySession()
        {
            yield return new WaitForSeconds(2f);

            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                yield break;
            }

            if (!RtsNetplaySession.IsNetworkMatch)
            {
                Debug.LogError(
                    "[MP] Chưa có session Mirror. Mở scene "
                    + "'Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity' "
                    + "→ Play → Host (hoặc Client IP:7777) → Ready → Bắt đầu trận.");
            }
        }

        IEnumerator RefreshAfterNetworkReady()
        {
            if (NetworkServer.active)
            {
                RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(
                    FindObjectsInactive.Include);
                if (setup != null)
                {
                    RtsUtsServerEntityFactory.EnsureAllGameplayPrefabsRegistered(setup);
                }

                RtsMatchServerSpawnRunner.EnsureScheduled();
                GameMatchOverlayStateSync.EnsureServerInstance();
                RtsUtsSupplyStateSync.EnsureServerInstance();
                RtsUtsTechStateRelay.EnsureServerInstance();
                RtsGatherableSupplyNetworkSync.EnsureServerInstance();
            }

            yield return null;

            MpLocalOwnerSceneSync.RefreshAfterGameSceneLoad();
            MpFogVisionSpawnRefresh.SchedulePresentationRetries();
            StartCoroutine(RetryPureClientPresentation());

            if (disableCapsuleGameInput && NetworkClient.localPlayer != null)
            {
                RtsGameInput capsuleInput = NetworkClient.localPlayer.GetComponent<RtsGameInput>();
                if (capsuleInput != null)
                {
                    capsuleInput.enabled = false;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Pure client P2 — HUD đôi khi chưa bind sau frame đầu (LocalOwner / rig / SyncVar).
        /// Cách hoạt động: Retry presentation vài lần sau load Game 1/2.
        /// </summary>
        IEnumerator RetryPureClientPresentation()
        {
            if (!RtsNetplaySession.IsPureClient)
            {
                yield break;
            }

            const int attempts = 3;
            for (int i = 0; i < attempts; i++)
            {
                yield return new WaitForSeconds(0.5f);

                if (!GameplayStartupScenes.IsActiveGameplayScene())
                {
                    yield break;
                }

                if (!MpLocalOwnerSceneSync.EnsureLocalOwnerInitialized(out Owner localOwner)
                    || localOwner != Owner.Player2)
                {
                    continue;
                }

                MpPresentationHudGuard.ApplyLocalHudBranch(localOwner);
                LocalHumanPresentationRefresh.RefreshFromLocalOwner();
                RtsUtsSupplyStateSync.ClientRefreshLocalSuppliesIfReady();
                MpRuntimeUiCoordinator.RefreshFullPresentationForOwner(localOwner);

                MpPlayerPresentationDirector director =
                    MpFogRefreshThrottle.ResolvePresentationDirector();
                if (director != null && CountConfiguredRuntimeUi(localOwner) > 0)
                {
                    yield break;
                }
            }
        }

        static int CountConfiguredRuntimeUi(Owner owner)
        {
            RuntimeUI[] runtimeUis = Object.FindObjectsByType<RuntimeUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui != null && ui.IsConfiguredForOwner(owner))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
