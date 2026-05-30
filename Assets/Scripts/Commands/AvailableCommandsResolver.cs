using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

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

        /// <summary>
        /// Mục tiêu: Chuột phải chọn đúng Gather/Attack (không bị Move.CanHandle luôn true cướp lệnh).
        /// Cách hoạt động: Ưu tiên Gather → Attack → lệnh khác (Move cuối).
        /// </summary>
        public static bool TryPickPrimaryRightClickCommand(
            AbstractUnit unit,
            RaycastHit hit,
            int unitIndex,
            MouseButton mouseButton,
            out BaseCommand command)
        {
            command = null;
            if (unit == null)
            {
                return false;
            }

            CommandContext context = new(unit, hit, unitIndex, mouseButton);
            List<BaseCommand> commands = GetFlattened(unit);

            if (TryPickFirstMatching<GatherCommand>(commands, context, out command)
                || TryPickFirstMatching<AttackCommand>(commands, context, out command))
            {
                return true;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                BaseCommand candidate = commands[i];
                if (candidate is GatherCommand or AttackCommand)
                {
                    continue;
                }

                if (candidate != null && candidate.CanHandle(context))
                {
                    command = candidate;
                    return true;
                }
            }

            return false;
        }

        static bool TryPickFirstMatching<TCommand>(
            List<BaseCommand> commands,
            CommandContext context,
            out BaseCommand command)
            where TCommand : BaseCommand
        {
            command = null;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is TCommand typed && typed.CanHandle(context))
                {
                    command = typed;
                    return true;
                }
            }

            return false;
        }
    }
}
