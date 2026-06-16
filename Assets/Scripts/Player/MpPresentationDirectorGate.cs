using GameDevTV.RTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Tránh PlayerViewBinder / FactionHudBinder ghi đè HUD MP khi <see cref="MpPlayerPresentationDirector"/> đã wire rig.
    /// </summary>
    public static class MpPresentationDirectorGate
    {
        /// <summary>
        /// Mục tiêu: Scene Game 1/2 MP — chỉ Director bind fog/HUD theo rig P1/P2.
        /// </summary>
        public static bool ShouldDeferHudAndFogToDirector()
        {
            if (!RtsNetplaySession.IsNetworkMatch)
            {
                return false;
            }

            MpPlayerPresentationDirector director = Object.FindFirstObjectByType<MpPlayerPresentationDirector>(
                FindObjectsInactive.Include);
            return director != null && director.HasConfiguredRigs;
        }
    }
}
