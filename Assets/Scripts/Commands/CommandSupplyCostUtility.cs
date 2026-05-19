using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Lấy chi phí / nhãn hành động từ các lệnh build & research (SRP cho cảnh báo tài nguyên).
    /// </summary>
    public static class CommandSupplyCostUtility
    {
        public static SupplyCostSO TryGetCost(BaseCommand command)
        {
            return command switch
            {
                BuildUnitCommand buildUnit => buildUnit.Unit?.Cost,
                BuildBuildingCommand buildBuilding => buildBuilding.Building?.Cost,
                ResearchUpgradeCommand research => research.Upgrade?.Cost,
                _ => null
            };
        }

        public static string GetActionDescription(BaseCommand command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            return command switch
            {
                BuildUnitCommand buildUnit when buildUnit.Unit != null =>
                    $"sản xuất {buildUnit.Unit.Name}",
                BuildBuildingCommand buildBuilding when buildBuilding.Building != null =>
                    $"xây {buildBuilding.Building.Name}",
                ResearchUpgradeCommand research when research.Upgrade != null =>
                    $"nghiên cứu {research.Upgrade.Name}",
                _ => command.Name
            };
        }
    }
}
