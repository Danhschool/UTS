using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Quy tắc unit/building human nào phát tín hiệu fog vision lên client.
    /// </summary>
    public static class HumanFogVisionUtility
    {
        /// <summary>
        /// Mục tiêu: Chỉ Player1/Player2 bật VisionTransform trên client human.
        /// Cách hoạt động: Ủy quyền <see cref="OwnerTeamMapping.IsHumanPlayer"/>.
        /// </summary>
        public static bool EmitsFogVision(Owner owner) =>
            OwnerTeamMapping.IsHumanPlayer(owner);
    }
}
