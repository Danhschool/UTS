using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Truy cập LocalOwner cho HUD/input mà không hardcode Player1.
    /// </summary>
    public static class LocalHumanOwnerAccess
    {
        /// <summary>
        /// Mục tiêu: Owner human đang điều khiển trên máy này (offline mặc định Player1).
        /// Cách hoạt động: Đọc <see cref="LocalHumanOwnerService"/> nếu đã init; ngược lại Player1.
        /// </summary>
        public static Owner GetLocalOwnerOrDefault()
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service != null && service.IsInitialized)
            {
                return service.LocalOwner;
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
