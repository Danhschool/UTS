using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chặn AI gán lệnh macro khi worker đang xây hoặc đang gather/return — tránh phá Behavior micro.
    /// </summary>
    public static class AIWorkerCommandGuard
    {
        /// <summary>
        /// Mục tiêu: Worker có thể nhận lệnh AI macro mới hay không.
        /// Cách hoạt động: Null-safe; false khi <see cref="Worker.IsBuilding"/> hoặc <see cref="Worker.IsGatheringOrReturning"/>.
        /// </summary>
        public static bool CanAssignCommand(Worker worker) =>
            worker != null && !worker.IsBuilding && !worker.IsGatheringOrReturning;

        /// <summary>
        /// Mục tiêu: Worker có thể nhận GatherCommand tới một mỏ (BT tự loop nếu trùng node đã gán — xem <see cref="ShouldDispatchGatherToSupply"/>).
        /// Cách hoạt động: Không build, không đang mang tài nguyên về kho.
        /// </summary>
        public static bool CanAssignGatherCommand(Worker worker) =>
            worker != null
            && !worker.IsCommittedToConstructionWork
            && !worker.HasSupplies;

        /// <summary>
        /// Mục tiêu: Không dispatch Gather trùng node đã khóa; cho phép chuyển sang mỏ khác.
        /// Cách hoạt động: <see cref="Worker.ShouldIssueGatherTo"/>; collider con vẫn resolve qua GetComponentInParent.
        /// </summary>
        public static bool ShouldDispatchGatherToSupply(Worker worker, RaycastHit hit)
        {
            if (!CanAssignGatherCommand(worker))
            {
                return false;
            }

            GatherableSupply supply = hit.collider.GetComponentInParent<GatherableSupply>();
            if (supply == null)
            {
                return true;
            }

            return worker.ShouldIssueGatherTo(supply);
        }

        /// <summary>
        /// Mục tiêu: Worker mang tài nguyên có thể nhận lệnh nộp kho (player hoặc fallback thủ công).
        /// Cách hoạt động: HasSupplies và không build; Petra economy thường để BT tự return.
        /// </summary>
        public static bool CanAssignReturnSuppliesCommand(Worker worker) =>
            worker != null && !worker.IsBuilding && worker.HasSupplies;

        /// <summary>
        /// Mục tiêu: Dispatcher được gửi Stop khi worker đang gather/return — ngắt BT trước build.
        /// Cách hoạt động: Chỉ chặn khi đang xây; không dùng <see cref="CanAssignCommand"/>.
        /// </summary>
        public static bool ShouldDispatchStopCommand(Worker worker) =>
            worker != null && !worker.IsBuilding;

        /// <summary>
        /// Mục tiêu: Dispatcher được gửi Build cho worker base đã gán tick này (sau Stop cùng tick).
        /// Cách hoạt động: Rảnh hoặc designated builder; không chặn vì đang gather nếu đã AssignBuilder.
        /// </summary>
        public static bool ShouldDispatchBuildBuildingCommand(Worker worker)
        {
            if (worker == null || worker.IsBuilding)
            {
                return false;
            }

            if (AIConstructionAssignment.ShouldSkipGatherForWorker(worker.GetInstanceID()))
            {
                return true;
            }

            return CanAssignCommand(worker);
        }

        /// <summary>
        /// Mục tiêu: Planner chỉ enqueue intent hợp lệ — đặc biệt với worker.
        /// Cách hoạt động: Gather + HasSupplies → return; Gather → gather; còn lại → <see cref="CanAssignCommand"/>.
        /// </summary>
        public static bool ShouldEnqueue(AICommandIntent intent)
        {
            if (intent.Entity == null)
            {
                return false;
            }

            if (intent.Entity is Worker worker)
            {
                if (AIEconomyPriority.IsGatherRefreshPriority(intent.Priority))
                {
                    return ShouldEnqueueGatherRefresh(worker, intent.Command);
                }

                if (intent.Command is GatherCommand)
                {
                    if (AIConstructionAssignment.ShouldSkipGatherForWorker(worker))
                    {
                        return false;
                    }

                    return worker.HasSupplies
                        ? CanAssignReturnSuppliesCommand(worker)
                        : ShouldDispatchGatherToSupply(worker, intent.Hit);
                }

                if (intent.Command is StopCommand)
                {
                    return ShouldDispatchStopCommand(worker);
                }

                if (intent.Command is BuildBuildingCommand)
                {
                    return ShouldDispatchBuildBuildingCommand(worker);
                }

                return CanAssignCommand(worker);
            }

            return true;
        }

        /// <summary>
        /// Mục tiêu: Cho phép Stop/Move refresh khi worker đang gather (bình thường bị chặn).
        /// Cách hoạt động: Chỉ khi không build; Stop hoặc Move từ economy refresh band.
        /// </summary>
        private static bool ShouldEnqueueGatherRefresh(Worker worker, BaseCommand command) =>
            worker != null
            && !worker.IsBuilding
            && !worker.IsCommittedToConstructionWork
            && (command is StopCommand || command is MoveCommand);
    }
}
