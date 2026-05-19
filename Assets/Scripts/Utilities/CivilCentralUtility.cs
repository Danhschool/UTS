using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Identifies the main base (Civil Central) for win/lose and supply-deposit rules.
    /// </summary>
    public static class CivilCentralUtility
    {
        /// <summary>
        /// Mục tiêu: Xác định building có phải nhà chính (Civil Central) hay không.
        /// Cách hoạt động: So tên hiển thị trên <see cref="BuildingSO"/> với hằng <see cref="SupplyDepositLocator.CivilCentralDisplayName"/>.
        /// </summary>
        public static bool IsCivilCentral(BaseBuilding building) =>
            building?.BuildingSO != null
            && building.BuildingSO.Name == SupplyDepositLocator.CivilCentralDisplayName;
    }
}
