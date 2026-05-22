using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Tuần tra lẻ quanh CC trong vòng bán kính — không formation, mỗi lính một điểm Move riêng.
    /// </summary>
    public static class AIMilitaryLoosePatrolPlanner
    {
        /// <summary>
        /// Mục tiêu: Gán Move rời trong [minRadius, maxRadius] quanh CC; chỉ unit trong operational leash.
        /// </summary>
        public static void EnqueueLoosePatrolIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> assignedUnitIds,
            int patrolPhase,
            Vector3 civilCentralPosition,
            float minPatrolRadius,
            float maxPatrolRadius,
            float operationalLeashRadius,
            int maxAssignmentsPerTick,
            System.Func<AbstractUnit, MoveCommand> resolveMoveCommand)
        {
            if (snapshot == null
                || queue == null
                || militaryUnits == null
                || resolveMoveCommand == null
                || maxAssignmentsPerTick <= 0
                || militaryUnits.Count == 0)
            {
                return;
            }

            bool useLeash = operationalLeashRadius < float.MaxValue * 0.5f;
            int assigned = 0;
            int count = militaryUnits.Count;
            int start = patrolPhase % count;

            for (int offset = 0; offset < count && assigned < maxAssignmentsPerTick; offset++)
            {
                AbstractUnit unit = militaryUnits[(start + offset) % count];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || assignedUnitIds != null && assignedUnitIds.Contains(unit.GetInstanceID())
                    || !AIMilitaryPatrolUtility.IsEligibleForLoosePatrol(unit))
                {
                    continue;
                }

                if (useLeash
                    && !AIMilitaryOperationalLeash.IsWithinOperationalZone(
                        unit.transform.position,
                        civilCentralPosition,
                        operationalLeashRadius))
                {
                    continue;
                }

                MoveCommand move = resolveMoveCommand(unit);
                if (move == null)
                {
                    continue;
                }

                int scatterSeed = unit.GetInstanceID() ^ (patrolPhase + assigned * 131);
                if (!AIMilitaryPatrolUtility.TryGetLoosePatrolDestination(
                        civilCentralPosition,
                        scatterSeed,
                        minPatrolRadius,
                        maxPatrolRadius,
                        out Vector3 destination))
                {
                    continue;
                }

                if (useLeash
                    && !AIMilitaryOperationalLeash.IsWithinOperationalZone(
                        destination,
                        civilCentralPosition,
                        operationalLeashRadius))
                {
                    continue;
                }

                queue.Enqueue(new AICommandIntent(
                    AIMilitaryPriority.PatrolExpandMap - assigned,
                    AIManagerIds.Military,
                    unit,
                    move,
                    AIHitUtility.AtPoint(destination),
                    mouseButton: MouseButton.Right));

                assignedUnitIds?.Add(unit.GetInstanceID());
                assigned++;
            }
        }
    }
}
