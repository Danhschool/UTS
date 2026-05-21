using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Cache <see cref="BuildUnitCommand"/> trong project — fallback khi Barrack/Inspector chưa gán SO.
    /// </summary>
    public static class AIBuildUnitCommandCatalog
    {
        private static BuildUnitCommand[] cachedCommands;

        /// <summary>
        /// Mục tiêu: Bổ sung lệnh train quân (không Worker) vào danh sách planner.
        /// Cách hoạt động: FindObjectsByTypeAll một lần; lọc tên unit không chứa "Worker".
        /// </summary>
        public static void AppendMilitaryTrainCommands(List<BuildUnitCommand> output)
        {
            if (output == null)
            {
                return;
            }

            EnsureCache();
            for (int i = 0; i < cachedCommands.Length; i++)
            {
                BuildUnitCommand command = cachedCommands[i];
                if (!IsMilitaryTrainCommand(command) || output.Contains(command))
                {
                    continue;
                }

                output.Add(command);
            }
        }

        private static void EnsureCache()
        {
            if (cachedCommands != null)
            {
                return;
            }

            cachedCommands = Object.FindObjectsByType<BuildUnitCommand>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static bool IsMilitaryTrainCommand(BuildUnitCommand command)
        {
            if (command?.Unit == null)
            {
                return false;
            }

            string name = command.Unit.Name;
            return !string.IsNullOrEmpty(name)
                && !name.Contains("Worker", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
