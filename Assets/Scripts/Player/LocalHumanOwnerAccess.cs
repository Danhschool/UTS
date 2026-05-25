using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Truy cập LocalOwner cho HUD/input mà không hardcode Player1.
    /// </summary>
    public static class LocalHumanOwnerAccess
    {
        /// <summary>
        /// Mục tiêu: Owner human trên máy này; MP chưa init thì dùng team cache lobby, không ép P1.
        /// </summary>
        public static Owner GetLocalOwnerOrDefault()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service != null && service.IsInitialized)
            {
                return service.LocalOwner;
            }

            if (NetworkClient.active)
            {
                int team = MpLocalOwnerSceneSync.ResolveLocalTeamIndex();
                if (team >= 0)
                {
                    return OwnerTeamMapping.FromTeamIndex(team);
                }
            }

            return Owner.Player1;
        }

        public static bool IsLocalOwner(Owner owner)
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            return service != null && service.IsInitialized && service.IsLocalOwner(owner);
        }
    }
}
