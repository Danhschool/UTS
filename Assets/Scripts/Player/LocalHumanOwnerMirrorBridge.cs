using GameDevTV.RTS.Netplay;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// DIP: Lobby Mirror → <see cref="LocalHumanOwnerService"/> + refresh presentation.
    /// </summary>
    public static class LocalHumanOwnerMirrorBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsLocalHumanOwnerNotifier.OnLocalTeamIndex += OnLocalTeamIndex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOfflineService()
        {
            if (NetworkClient.active)
            {
                return;
            }

            LocalHumanOwnerService.EnsureExists();
        }

        static void OnLocalTeamIndex(int teamIndex)
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.EnsureExists();
            service.SetLocalOwner(OwnerTeamMapping.FromTeamIndex(teamIndex));
            LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            MpFogVisionSpawnRefresh.SchedulePresentationRetries();
        }
    }
}
