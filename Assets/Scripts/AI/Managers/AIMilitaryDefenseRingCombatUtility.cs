using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Combat trong vùng leash — đánh địch thấy ngay; Stop khi unit ra khỏi vùng.
    /// </summary>
    public static class AIMilitaryDefenseRingCombatUtility
    {
        /// <summary>
        /// Mục tiêu: Lính trong leash tấn công địch gần nhất trong leash.
        /// </summary>
        public static int EnqueueAttacksWithinOperationalZone(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Owner friendlyOwner,
            Vector3 civilCentralPosition,
            float operationalRadius,
            bool requireVisible,
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> assignedUnitIds,
            int maxAssignments)
        {
            if (snapshot == null || queue == null || militaryUnits == null)
            {
                return 0;
            }

            List<IDamageable> threats = AIMilitaryHostileScanner.ThreatScratchList;
            AIMilitaryHostileScanner.CollectThreatsNearCivilCentral(
                friendlyOwner,
                civilCentralPosition,
                operationalRadius,
                requireVisible,
                threats);

            int assigned = 0;
            for (int i = 0; i < militaryUnits.Count && assigned < maxAssignments; i++)
            {
                AbstractUnit unit = militaryUnits[i];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || assignedUnitIds != null && assignedUnitIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (!AIMilitaryOperationalLeash.IsWithinOperationalZone(
                        unit.transform.position,
                        civilCentralPosition,
                        operationalRadius))
                {
                    continue;
                }

                AttackCommand attack = ResolveAttackCommand(unit);
                if (attack == null)
                {
                    continue;
                }

                if (!AIMilitaryHostileScanner.TryPickClosestThreat(
                        unit.transform.position,
                        friendlyOwner,
                        requireVisible,
                        threats,
                        out IDamageable threat)
                    || !TryCreateHostileHit(threat, out RaycastHit hit))
                {
                    continue;
                }

                queue.Enqueue(new AICommandIntent(
                    AIMilitaryPriority.AttackThreatNearCc - assigned,
                    AIManagerIds.Military,
                    unit,
                    attack,
                    hit,
                    mouseButton: MouseButton.Right));
                assignedUnitIds?.Add(unit.GetInstanceID());
                assigned++;
            }

            return assigned;
        }

        /// <summary>
        /// Mục tiêu: Unit đã chạy ra ngoài leash → Stop (không truy đuổi).
        /// </summary>
        public static int EnqueueStopForUnitsOutsideOperationalZone(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Vector3 civilCentralPosition,
            float operationalRadius,
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> assignedUnitIds)
        {
            if (snapshot == null || queue == null || militaryUnits == null)
            {
                return 0;
            }

            int stopped = 0;
            for (int i = 0; i < militaryUnits.Count; i++)
            {
                AbstractUnit unit = militaryUnits[i];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || assignedUnitIds != null && assignedUnitIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (AIMilitaryOperationalLeash.IsWithinOperationalZone(
                        unit.transform.position,
                        civilCentralPosition,
                        operationalRadius))
                {
                    continue;
                }

                StopCommand stop = ResolveStopCommand(unit);
                if (stop == null)
                {
                    unit.Stop();
                    continue;
                }

                queue.Enqueue(new AICommandIntent(
                    AIMilitaryPriority.PatrolExpandMap - 5 - stopped,
                    AIManagerIds.Military,
                    unit,
                    stop,
                    AIHitUtility.AtPoint(unit.transform.position),
                    mouseButton: MouseButton.Left));
                assignedUnitIds?.Add(unit.GetInstanceID());
                stopped++;
            }

            return stopped;
        }

        private static bool TryCreateHostileHit(IDamageable hostile, out RaycastHit hit)
        {
            hit = default;
            if (hostile?.Transform == null)
            {
                return false;
            }

            Collider collider = hostile.Transform.GetComponent<Collider>()
                ?? hostile.Transform.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                hit = AIHitUtility.AtPoint(hostile.Transform.position);
                return true;
            }

            return AIHitUtility.TryCreateHit(collider, out hit);
        }

        private static AttackCommand ResolveAttackCommand(AbstractUnit unit)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is AttackCommand attack)
                {
                    return attack;
                }
            }

            return null;
        }

        private static StopCommand ResolveStopCommand(AbstractUnit unit)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is StopCommand stop)
                {
                    return stop;
                }
            }

            return null;
        }
    }
}
