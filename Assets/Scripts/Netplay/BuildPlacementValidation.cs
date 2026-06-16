using GameDevTV.RTS.Commands;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server kiểm tra tech + vị trí đặt nhà trước khi trừ tài nguyên.
    /// </summary>
    public static class BuildPlacementValidation
    {
        /// <summary>
        /// Mục tiêu: Tránh P2/client gửi lệnh build hợp lệ trên UI nhưng server spawn fail hoặc trừ tiền oan.
        /// Cách hoạt động: TechTree.IsUnlocked + AllRestrictionsPass từ BuildBuildingCommand khớp BuildingSO.
        /// </summary>
        public static bool TryValidateServerBuild(
            Worker worker,
            BuildingSO buildingSo,
            Owner owner,
            Vector3 worldPoint,
            out string failReason)
        {
            failReason = null;
            if (buildingSo == null)
            {
                failReason = "building_null";
                return false;
            }

            if (!buildingSo.TechTree.IsUnlocked(owner, buildingSo))
            {
                failReason = "tech_locked";
                return false;
            }

            if (!TryFindBuildCommand(worker, buildingSo, out BuildBuildingCommand buildCommand))
            {
                return true;
            }

            if (!buildCommand.AllRestrictionsPass(worldPoint))
            {
                failReason = "placement_invalid";
                return false;
            }

            return true;
        }

        static bool TryFindBuildCommand(Worker worker, BuildingSO buildingSo, out BuildBuildingCommand buildCommand)
        {
            buildCommand = null;
            if (worker == null || buildingSo == null)
            {
                return false;
            }

            foreach (BaseCommand command in AvailableCommandsResolver.GetFlattened(worker))
            {
                if (command is BuildBuildingCommand build && build.Building == buildingSo)
                {
                    buildCommand = build;
                    return true;
                }
            }

            return false;
        }
    }
}
