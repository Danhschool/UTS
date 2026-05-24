using GameDevTV.RTS.Netplay;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// DIP: Kết nối callback Mirror (assembly Netplay) với <see cref="LocalHumanOwnerService"/> (game).
    /// </summary>
    public static class LocalHumanOwnerMirrorBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsLocalHumanOwnerNotifier.OnLocalTeamIndex = OnLocalTeamIndex;
        }

        /// <summary>
        /// Mục tiêu: Offline (Game 1) có service = Player1 dù scene chưa gắn <see cref="LocalHumanOwnerBootstrap"/>.
        /// Cách hoạt động: Sau load scene, nếu không Mirror thì <see cref="LocalHumanOwnerService.EnsureExists"/> (Awake gán P1).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOfflineService()
        {
            if (NetworkClient.active)
                return;

            LocalHumanOwnerService.EnsureExists();
        }

        static void OnLocalTeamIndex(int teamIndex)
        {
            LocalHumanOwnerService.EnsureExists()
                .SetLocalOwner(OwnerTeamMapping.FromTeamIndex(teamIndex));
        }
    }
}
