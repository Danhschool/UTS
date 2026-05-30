using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// SRP: Lấy lệnh theo slot (0–8) cho selection hiện tại — cùng quy tắc với <see cref="Containers.ActionsUI"/>.
    /// </summary>
    public static class ActionBarCommandResolver
    {
        /// <summary>
        /// Mục tiêu: Trả về lệnh gắn slot trên thanh action (phím 1 = slot 0, …, 9 = slot 8).
        /// Cách hoạt động: Giao lệnh khả dụng của unit đầu, intersect với các unit còn lại, lọc theo <see cref="BaseCommand.Slot"/>.
        /// </summary>
        public static bool TryGetCommandForSlot(
            IEnumerable<AbstractCommandable> selectedUnits,
            int slotIndex,
            out BaseCommand command)
        {
            command = null;
            if (selectedUnits == null || slotIndex < 0)
            {
                return false;
            }

            List<AbstractCommandable> units = selectedUnits as List<AbstractCommandable>
                ?? selectedUnits.ToList();
            if (units.Count == 0)
            {
                return false;
            }

            AbstractCommandable first = units[0];
            Owner busOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            IEnumerable<BaseCommand> availableCommands = (first.AvailableCommands ?? System.Array.Empty<BaseCommand>())
                .Where(action => action.IsAvailable(new CommandContext(busOwner, first, new RaycastHit())));

            for (int i = 1; i < units.Count; i++)
            {
                AbstractCommandable commandable = units[i];
                if (commandable.AvailableCommands != null)
                {
                    availableCommands = availableCommands.Intersect(commandable.AvailableCommands);
                }
            }

            command = availableCommands.FirstOrDefault(action => action.Slot == slotIndex);
            return command != null;
        }
    }
}
