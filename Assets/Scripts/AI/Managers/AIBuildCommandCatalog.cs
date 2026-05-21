using GameDevTV.RTS.Commands;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Cache toàn bộ <see cref="BuildBuildingCommand"/> trong project (một lần) — fallback khi worker/Inspector chưa gán SO.
    /// </summary>
    public static class AIBuildCommandCatalog
    {
        private static BuildBuildingCommand[] cachedCommands;

        /// <summary>
        /// Mục tiêu: Tìm lệnh build theo <see cref="BuildingSO.Name"/> khi resolver thường trả null.
        /// Cách hoạt động: FindObjectsByTypeAll một lần; so khớp tên nhà chuẩn (Store House, Corral, …).
        /// </summary>
        public static BuildBuildingCommand FindByBuildingDisplayName(string buildingDisplayName)
        {
            if (string.IsNullOrEmpty(buildingDisplayName))
            {
                return null;
            }

            EnsureCache();
            for (int i = 0; i < cachedCommands.Length; i++)
            {
                BuildBuildingCommand command = cachedCommands[i];
                if (command?.Building != null && command.Building.Name == buildingDisplayName)
                {
                    return command;
                }
            }

            return null;
        }

        private static void EnsureCache()
        {
            if (cachedCommands != null)
            {
                return;
            }

            cachedCommands = Object.FindObjectsByType<BuildBuildingCommand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
