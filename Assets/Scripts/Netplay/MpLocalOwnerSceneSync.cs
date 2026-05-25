using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Sau load RtsNet_Game — gán LocalOwner từ RtsLobbyPlayer và bật presentation rig.
    /// </summary>
    public static class MpLocalOwnerSceneSync
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsMatchSceneClientNotifier.OnGameSceneLoaded += RefreshAfterGameSceneLoad;
        }

        public static void RefreshAfterGameSceneLoad()
        {
            if (!NetworkClient.active)
            {
                return;
            }

            if (TryApplyTeamIndex(ResolveLocalTeamIndex()))
            {
                return;
            }

            LocalHumanPresentationRefresh.RefreshFromLocalOwner();
        }

        public static bool TryApplyTeamIndex(int teamIndex)
        {
            if (teamIndex < 0)
            {
                return false;
            }

            RtsLocalHumanOwnerNotifier.NotifyLocalTeamIndex(teamIndex);
            return true;
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo LocalOwner đã init trước khi Director/UI/fog chạy.
        /// </summary>
        public static bool EnsureLocalOwnerInitialized(out Owner localOwner)
        {
            localOwner = Owner.Invalid;
            LocalHumanOwnerService service = LocalHumanOwnerService.EnsureExists();
            int resolvedTeam = ResolveLocalTeamIndex();

            if (!service.IsInitialized)
            {
                TryApplyTeamIndex(resolvedTeam);
            }

            if (!service.IsInitialized)
            {
                return false;
            }

            localOwner = service.LocalOwner;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Lobby player → cache; client thuần MP = team 1; host = team 0.
        /// </summary>
        public static int ResolveLocalTeamIndex()
        {
            if (NetworkClient.localPlayer != null)
            {
                RtsLobbyPlayer lobbyPlayer = NetworkClient.localPlayer.GetComponent<RtsLobbyPlayer>();
                if (lobbyPlayer != null)
                {
                    return lobbyPlayer.PlayerTeamIndex;
                }
            }

            if (RtsLocalHumanOwnerNotifier.CachedLocalTeamIndex >= 0)
            {
                return RtsLocalHumanOwnerNotifier.CachedLocalTeamIndex;
            }

            return -1;
        }
    }
}
