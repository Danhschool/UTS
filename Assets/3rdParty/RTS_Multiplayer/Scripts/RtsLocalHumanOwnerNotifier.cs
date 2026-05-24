using System;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// DIP: Hook không phụ thuộc Assembly-CSharp — game đăng ký map team → UTS Owner.
    /// </summary>
    public static class RtsLocalHumanOwnerNotifier
    {
        public static Action<int> OnLocalTeamIndex;

        public static void NotifyLocalTeamIndex(int teamIndex) =>
            OnLocalTeamIndex?.Invoke(teamIndex);
    }
}
