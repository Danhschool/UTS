using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Converts game bus events into player-facing log strings.
    /// </summary>
    public static class GameEventLogMessageFormatter
    {
        private const int MinSupplyChangeToLog = 25;

        public static bool TryFormatSupply(SupplyEvent evt, out string message, out GameEventLogCategory category)
        {
            message = null;
            category = GameEventLogCategory.Resource;

            if (evt.Supply == null || System.Math.Abs(evt.Amount) < MinSupplyChangeToLog)
            {
                return false;
            }

            string supplyName = evt.Supply.name;
            if (evt.Amount < 0)
            {
                message = $"Đã dùng {-evt.Amount} {supplyName}.";
                return true;
            }

            message = $"Nhận +{evt.Amount} {supplyName}.";
            return true;
        }

        public static string FormatBuildingCompleted(BaseBuilding building) =>
            $"Xây xong: {GetBuildingName(building)}.";

        public static string FormatBuildingLost(BaseBuilding building) =>
            $"Mất công trình: {GetBuildingName(building)}.";

        public static string FormatUnitLost(AbstractUnit unit) =>
            $"Mất đơn vị: {GetUnitName(unit)}.";

        public static string FormatUpgrade(UpgradeSO upgrade) =>
            $"Nghiên cứu xong: {upgrade.Name}.";

        public static string FormatConstructionStarted() =>
            "Bắt đầu xây dựng công trình.";

        private static string GetBuildingName(BaseBuilding building)
        {
            if (building?.BuildingSO != null)
            {
                return building.BuildingSO.Name;
            }

            return building != null ? building.name : "Building";
        }

        private static string GetUnitName(AbstractUnit unit)
        {
            if (unit?.UnitSO is UnitSO unitSo)
            {
                return unitSo.Name;
            }

            return unit != null ? unit.name : "Unit";
        }
    }
}
