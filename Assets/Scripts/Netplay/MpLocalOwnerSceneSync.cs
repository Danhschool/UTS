using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Sau load map Game 1/2 — gán LocalOwner từ RtsLobbyPlayer và bật presentation rig.
    /// </summary>
    public static class MpLocalOwnerSceneSync
    {
        const int MinFramesBetweenSceneRefresh = 90;
        static int _lastSceneRefreshFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsMatchSceneClientNotifier.OnGameSceneLoaded += RefreshAfterGameSceneLoad;
        }

        /// <summary>
        /// Mục tiêu: Tránh gọi Apply presentation 3–4 lần khi load scene (lag + log trùng).
        /// Cách hoạt động: Debounce theo frame; bootstrap coroutine gọi một lần sau network ready.
        /// </summary>
        public static void RefreshAfterGameSceneLoad()
        {
            if (!NetworkClient.active)
            {
                return;
            }

            int frame = Time.frameCount;
            if (_lastSceneRefreshFrame >= 0 && frame - _lastSceneRefreshFrame < MinFramesBetweenSceneRefresh)
            {
                return;
            }

            _lastSceneRefreshFrame = frame;
            MpFogRefreshThrottle.InvalidateCaches();

            int teamIndex = ResolveLocalTeamIndex();
            ApplyTeamAndPresentation(teamIndex);
        }

        /// <summary>
        /// Mục tiêu: Client P2 vào RtsNet_Game — team đã cache ở lobby nhưng LocalOwnerService mới chưa init.
        /// Cách hoạt động: Notify team; ép SetLocalOwner nếu chưa khớp; luôn refresh presentation (không return sớm).
        /// </summary>
        public static void ApplyTeamAndPresentation(int teamIndex)
        {
            if (teamIndex < 0)
            {
                return;
            }

            TryApplyTeamIndex(teamIndex);
            EnsureLocalOwnerMatchesTeam(teamIndex);
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
        /// Mục tiêu: NotifyLocalTeamIndex bỏ qua khi cache trùng — scene mới vẫn phải gán lại Player2.
        /// </summary>
        public static void EnsureLocalOwnerMatchesTeam(int teamIndex)
        {
            if (teamIndex < 0)
            {
                return;
            }

            Owner expected = OwnerTeamMapping.FromTeamIndex(teamIndex);
            LocalHumanOwnerService service = LocalHumanOwnerService.EnsureExists();
            if (!service.IsInitialized || service.LocalOwner != expected)
            {
                service.SetLocalOwner(expected);
            }
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo LocalOwner đã init trước khi Director/UI/fog chạy.
        /// </summary>
        public static bool EnsureLocalOwnerInitialized(out Owner localOwner)
        {
            localOwner = Owner.Invalid;
            LocalHumanOwnerService service = LocalHumanOwnerService.EnsureExists();
            int resolvedTeam = ResolveLocalTeamIndex();

            if (resolvedTeam >= 0)
            {
                TryApplyTeamIndex(resolvedTeam);
                EnsureLocalOwnerMatchesTeam(resolvedTeam);
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
        /// Cách hoạt động: Client không chạy server luôn ưu tiên team 1 (1v1) — tránh SyncVar team 0 lệch trên clone.
        /// </summary>
        public static int ResolveLocalTeamIndex()
        {
            if (!NetworkClient.active && !NetworkServer.active)
            {
                return -1;
            }

            if (NetworkClient.active && !NetworkServer.active)
            {
                return 1;
            }

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

            return NetworkServer.active ? 0 : 1;
        }
    }
}
