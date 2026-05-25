using System.Collections;
using GameDevTV.RTS.Player;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Bootstrap RtsNet_Game — sync LocalOwner, presentation MP, tắt capsule input.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RtsNetGameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "RtsNet_Game";
        [SerializeField] bool disableCapsuleGameInput = true;

        void Awake()
        {
            if (!RtsNetSceneUtility.MatchesActiveScene(gameSceneName))
            {
                return;
            }

            LocalHumanOwnerService.EnsureExists();
            RtsLobbyUI.HideLobbyCanvasForGameplay();
        }

        void Start()
        {
            if (!RtsNetSceneUtility.MatchesActiveScene(gameSceneName))
            {
                return;
            }

            StartCoroutine(RefreshAfterNetworkReady());
        }

        IEnumerator RefreshAfterNetworkReady()
        {
            if (NetworkServer.active)
            {
                RtsMatchServerSpawnRunner.EnsureScheduled();
            }

            yield return null;

            MpLocalOwnerSceneSync.RefreshAfterGameSceneLoad();
            if (!NetworkServer.active)
            {
                MpFogVisionSpawnRefresh.SchedulePresentationRetries();
            }

            if (disableCapsuleGameInput && NetworkClient.localPlayer != null)
            {
                RtsGameInput capsuleInput = NetworkClient.localPlayer.GetComponent<RtsGameInput>();
                if (capsuleInput != null)
                {
                    capsuleInput.enabled = false;
                }
            }
        }
    }
}
