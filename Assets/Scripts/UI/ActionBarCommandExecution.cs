using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// SRP: Kiểm tra có thể kích hoạt lệnh từ thanh action / phím số hay không.
    /// </summary>
    public static class ActionBarCommandExecution
    {
        /// <summary>
        /// Mục tiêu: Cho phép bấm phím/UI khi thiếu tài nguyên thì cảnh báo thay vì im lặng.
        /// Cách hoạt động: Duyệt selection; IsAvailable + supply + không IsLocked → true nếu có ít nhất một unit hợp lệ.
        /// </summary>
        public static bool TryValidateForExecution(BaseCommand command, AbstractCommandable[] units)
        {
            if (command == null || units == null || units.Length == 0)
            {
                return false;
            }

            SupplyCostSO cost = CommandSupplyCostUtility.TryGetCost(command);
            string actionDescription = CommandSupplyCostUtility.GetActionDescription(command);
            bool anyCanExecute = false;

            for (int i = 0; i < units.Length; i++)
            {
                CommandContext context = new(units[i], new RaycastHit());
                if (!command.IsAvailable(context))
                {
                    continue;
                }

                if (cost != null && !SupplyAffordability.HasEnough(context.Owner, cost))
                {
                    SupplyAffordability.WarnPlayerIfInsufficient(context.Owner, cost, actionDescription);
                    continue;
                }

                if (!command.IsLocked(context))
                {
                    anyCanExecute = true;
                }
            }

            return anyCanExecute;
        }
    }
}
