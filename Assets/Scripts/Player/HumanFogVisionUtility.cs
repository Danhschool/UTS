using GameDevTV.RTS.Units;
using Mirror;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Quy tắc unit/building human nào phát tín hiệu fog vision trên client.
    /// </summary>
    public static class HumanFogVisionUtility
    {
        /// <summary>
        /// Mục tiêu: Phân biệt phe human (P1/P2) với AI/bot khi xử lý fog và lệnh.
        /// Cách hoạt động: So sánh owner với Player1 hoặc Player2.
        /// </summary>
        public static bool IsHumanPlayer(Owner owner) =>
            owner == Owner.Player1 || owner == Owner.Player2;

        /// <summary>
        /// Mục tiêu: Chỉ Player1/Player2 bật VisionTransform trên client human.
        /// Cách hoạt động: Alias của <see cref="IsHumanPlayer"/>.
        /// </summary>
        public static bool EmitsFogVision(Owner owner) => IsHumanPlayer(owner);

        /// <summary>
        /// Mục tiêu: MP — chỉ phe local vẽ vision lên RT fog của mình (P2 client không bật vision P1).
        /// Cách hoạt động: Khi có NetworkClient, so khớp <see cref="LocalHumanOwnerService.LocalOwner"/>.
        /// </summary>
        public static bool EmitsFogVisionOnThisClient(Owner owner)
        {
            if (!IsHumanPlayer(owner))
            {
                return false;
            }

            if (!NetworkClient.active)
            {
                return true;
            }

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            return service != null && service.IsInitialized && service.LocalOwner == owner;
        }
    }
}
