using System;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// DIP: Mirror lobby báo team index; game assembly gán LocalHumanOwnerService.
    /// </summary>
    public static class RtsLocalHumanOwnerNotifier
    {
        /// <summary>Team index đã biết trên máy này (0=P1, 1=P2), giữ qua load scene khi localPlayer chưa sẵn.</summary>
        public static int CachedLocalTeamIndex { get; private set; } = -1;

        public static event Action<int> OnLocalTeamIndex;

        public static void NotifyLocalTeamIndex(int teamIndex)
        {
            if (CachedLocalTeamIndex == teamIndex)
            {
                return;
            }

            CachedLocalTeamIndex = teamIndex;
            OnLocalTeamIndex?.Invoke(teamIndex);
        }

        public static void ClearCachedTeamIndex() => CachedLocalTeamIndex = -1;
    }
}
