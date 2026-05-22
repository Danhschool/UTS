using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Mỗi N tick AI — Stop rồi Move ngắn để worker thoát gather cũ; economy vẫn gán mỏ cho worker rảnh cùng tick.
    /// </summary>
    public static class AIWorkerGatherRefreshPlanner
    {
        private const float RefreshMoveNudgeDistance = 1.25f;

        /// <summary>
        /// Mục tiêu: Có phải tick reset gather worker hay không.
        /// Cách hoạt động: plannerTickIndex &gt; 0 và chia hết cho interval (mặc định 10).
        /// </summary>
        public static bool ShouldRefreshThisTick(int plannerTickIndex, int intervalTicks) =>
            intervalTicks > 0 && plannerTickIndex > 0 && plannerTickIndex % intervalTicks == 0;

        /// <summary>
        /// Mục tiêu: Enqueue Stop (cao) + Move (thấp hơn một chút) cho worker đang gather.
        /// Cách hoạt động: Bỏ qua build/return có tải; dispatch cùng tick, không enqueue gather.
        /// </summary>
        public static void EnqueueRefreshIntents(AIWorldStateSnapshot snapshot, AIPriorityQueue queue)
        {
            if (snapshot == null || queue == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (!ShouldRefreshWorker(worker))
                {
                    continue;
                }

                if (AIConstructionAssignment.ShouldSkipGatherForWorker(worker))
                {
                    continue;
                }

                StopCommand stop = ResolveStopCommand(worker);
                MoveCommand move = ResolveMoveCommand(worker);
                if (stop == null || move == null)
                {
                    continue;
                }

                Vector3 position = worker.transform.position;
                queue.Enqueue(new AICommandIntent(
                    AIEconomyPriority.WorkerGatherRefreshStop,
                    AIManagerIds.Economy,
                    worker,
                    stop,
                    AIHitUtility.AtPoint(position),
                    mouseButton: MouseButton.Left));

                Vector3 moveTarget = GetRefreshMoveTarget(worker);
                queue.Enqueue(new AICommandIntent(
                    AIEconomyPriority.WorkerGatherRefreshMove,
                    AIManagerIds.Economy,
                    worker,
                    move,
                    AIHitUtility.AtPoint(moveTarget),
                    mouseButton: MouseButton.Right));
            }
        }

        private static bool ShouldRefreshWorker(Worker worker) =>
            worker != null
            && !worker.IsCommittedToConstructionWork
            && !worker.HasSupplies
            && (worker.HasStaleGatherCommand || worker.IsGatheringOrReturning);

        private static Vector3 GetRefreshMoveTarget(Worker worker)
        {
            Vector3 origin = worker.transform.position;
            Vector3 forward = worker.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            return origin + forward.normalized * RefreshMoveNudgeDistance;
        }

        private static StopCommand ResolveStopCommand(Worker worker)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is StopCommand stop)
                {
                    return stop;
                }
            }

            return null;
        }

        private static MoveCommand ResolveMoveCommand(Worker worker)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is MoveCommand move)
                {
                    return move;
                }
            }

            return null;
        }
    }
}
