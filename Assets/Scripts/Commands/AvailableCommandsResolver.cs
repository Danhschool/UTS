using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Gom danh sách lệnh hiệu lực cho unit (override trước, rồi lệnh gốc), giống <see cref="GameDevTV.RTS.Player.PlayerInput"/>.
    /// </summary>
    public static class AvailableCommandsResolver
    {
        /// <summary>
        /// Mục tiêu: Một nơi duy nhất mô tả thứ tự lệnh sau override, tránh lệch giữa click phải và UI khác.
        /// Cách hoạt động: Lấy mọi <see cref="OverrideCommandsCommand"/> trên unit, nối <see cref="OverrideCommandsCommand.Commands"/>
        /// (bỏ lồng override), sau đó nối <see cref="AbstractCommandable.AvailableCommands"/> bỏ qua slot override.
        /// </summary>
        public static List<BaseCommand> GetFlattened(AbstractUnit unit)
        {
            OverrideCommandsCommand[] overrideCommandsCommands = unit.AvailableCommands
                .Where(command => command is OverrideCommandsCommand)
                .Cast<OverrideCommandsCommand>()
                .ToArray();

            List<BaseCommand> allAvailableCommands = new();
            foreach (OverrideCommandsCommand overrideCommand in overrideCommandsCommands)
            {
                allAvailableCommands.AddRange(overrideCommand.Commands
                    .Where(command => command is not OverrideCommandsCommand)
                );
            }

            allAvailableCommands.AddRange(unit.AvailableCommands
                .Where(command => command is not OverrideCommandsCommand)
            );

            return allAvailableCommands;
        }
    }
}
