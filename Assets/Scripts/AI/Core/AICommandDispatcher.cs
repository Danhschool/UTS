using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Thực thi lệnh gameplay qua <see cref="BaseCommand"/> — mirror <see cref="GameDevTV.RTS.Player.PlayerInput"/>, không UI/EventBus lệnh.
    /// </summary>
    public class AICommandDispatcher
    {
        private readonly Owner owner;

        public AICommandDispatcher(Owner owner)
        {
            this.owner = owner;
        }

        public Owner Owner => owner;

        /// <summary>
        /// Mục tiêu: Click phải AI — duyệt <see cref="AvailableCommandsResolver"/> giống người chơi.
        /// Cách hoạt động: Tạo <see cref="CommandContext"/> với owner AI; CanHandle → Handle; dừng nếu IsSingleUnitCommand.
        /// </summary>
        public bool DispatchUnitContext(
            AbstractUnit unit,
            RaycastHit hit,
            int unitIndex = 0,
            MouseButton mouseButton = MouseButton.Right)
        {
            if (unit == null || unit.Owner != owner)
            {
                return false;
            }

            if (unit is Worker worker && !PassesWorkerDispatchGuard(worker, null, hit))
            {
                return false;
            }

            CommandContext context = new(owner, unit, hit, unitIndex, mouseButton);
            List<BaseCommand> availableCommands = AvailableCommandsResolver.GetFlattened(unit);

            for (int i = 0; i < availableCommands.Count; i++)
            {
                ICommand command = availableCommands[i];
                if (!command.CanHandle(context))
                {
                    continue;
                }

                command.Handle(context);
                if (command.IsSingleUnitCommand)
                {
                    return true;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Planner đã chọn đúng một lệnh SO — bỏ vòng lặp flatten.
        /// Cách hoạt động: Guard worker; CanHandle → Handle một lần.
        /// </summary>
        public bool TryDispatchSpecificCommand(
            AbstractUnit unit,
            BaseCommand command,
            RaycastHit hit,
            int unitIndex = 0,
            MouseButton mouseButton = MouseButton.Right)
        {
            if (unit == null || command == null || unit.Owner != owner)
            {
                return false;
            }

            if (unit is Worker worker && !PassesWorkerDispatchGuard(worker, command, hit))
            {
                return false;
            }

            CommandContext context = new(owner, unit, hit, unitIndex, mouseButton);
            if (!command.CanHandle(context))
            {
                return false;
            }

            command.Handle(context);
            return true;
        }

        private static bool PassesWorkerDispatchGuard(Worker worker, BaseCommand command, RaycastHit hit)
        {
            if (command is GatherCommand)
            {
                if (AIConstructionAssignment.ShouldSkipGatherForWorker(worker))
                {
                    return false;
                }

                return worker.HasSupplies
                    ? AIWorkerCommandGuard.CanAssignReturnSuppliesCommand(worker)
                    : AIWorkerCommandGuard.ShouldDispatchGatherToSupply(worker, hit);
            }

            if (command is StopCommand)
            {
                return AIWorkerCommandGuard.ShouldDispatchStopCommand(worker);
            }

            if (command is BuildBuildingCommand)
            {
                return AIWorkerCommandGuard.ShouldDispatchBuildBuildingCommand(worker);
            }

            return AIWorkerCommandGuard.CanAssignCommand(worker);
        }

        /// <summary>
        /// Mục tiêu: Lệnh trên building (train, research) như nút Actions UI.
        /// Cách hoạt động: CommandContext với building; CanHandle → Handle.
        /// </summary>
        public bool DispatchBuildingCommand(BaseBuilding building, BaseCommand command, RaycastHit hit = default)
        {
            if (building == null || command == null || building.Owner != owner)
            {
                return false;
            }

            CommandContext context = new(owner, building, hit);
            if (!command.CanHandle(context))
            {
                return false;
            }

            command.Handle(context);
            return true;
        }
    }
}
