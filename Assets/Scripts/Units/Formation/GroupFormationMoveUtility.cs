using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Units.Formation
{
    /// <summary>
    /// SRP: Áp dụng Move cho nhóm unit theo formation vuông (player multi-select).
    /// </summary>
    public static class GroupFormationMoveUtility
    {
        /// <summary>
        /// Mục tiêu: Di chuyển nhiều unit một lần theo lưới vuông có phân hàng.
        /// Cách hoạt động: 1 unit → Move thường; nhiều unit → planner gán từng MoveTo.
        /// </summary>
        private const float DefaultReissueGoalTolerance = 18f;
        private const float ReissueSkipFraction = 0.65f;

        /// <summary>
        /// Mục tiêu: Formation move chỉ khi cần — tránh gửi lại cùng đích mỗi tick AI (chen chúc / Move vĩnh viễn).
        /// Cách hoạt động: Nếu đủ unit đang Move tới vùng đích thì bỏ qua; không thì <see cref="TryApplyMove"/>.
        /// </summary>
        public static bool TryApplyMoveIfNeeded(
            IReadOnlyList<AbstractUnit> units,
            RaycastHit hit,
            MoveCommand moveCommand,
            float goalMatchRadius = DefaultReissueGoalTolerance)
        {
            if (ShouldSkipFormationReissue(units, hit.point, goalMatchRadius))
            {
                return true;
            }

            return TryApplyMove(units, hit, moveCommand);
        }

        public static bool TryApplyMove(
            IReadOnlyList<AbstractUnit> units,
            RaycastHit hit,
            MoveCommand moveCommand)
        {
            if (units == null || units.Count == 0 || moveCommand == null)
            {
                return false;
            }

            if (TryMoveToVisibleTarget(units, hit))
            {
                return true;
            }

            float spacingMultiplier = moveCommand.FormationSpacingMultiplier;
            Vector3[] positions = UnitSquareFormationPlanner.ComputeWorldPositions(
                units,
                hit.point,
                spacingMultiplier);

            for (int i = 0; i < units.Count && i < positions.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                unit.MoveTo(positions[i]);
            }

            return true;
        }

        /// <summary>
        /// Mục tiêu: Đa số unit đã có lệnh Move tới vùng formation — không gửi lại mỗi tick AI.
        /// Cách hoạt động: So đích từng unit (ô lưới) với footprint quanh điểm click, không chỉ tâm một điểm.
        /// </summary>
        public static bool ShouldSkipFormationReissue(
            IReadOnlyList<AbstractUnit> units,
            Vector3 destination,
            float goalMatchRadius)
        {
            if (units == null || units.Count == 0)
            {
                return false;
            }

            float footprintRadius = EstimateFormationFootprintRadius(units);
            float goalTolerance = Mathf.Max(goalMatchRadius, footprintRadius);

            int pursuingNear = 0;
            int considered = 0;
            for (int i = 0; i < units.Count; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                considered++;
                if (IsUnitPursuingFormationMove(unit, destination, goalTolerance))
                {
                    pursuingNear++;
                }
            }

            if (considered == 0)
            {
                return false;
            }

            int required = Mathf.Max(1, Mathf.CeilToInt(considered * ReissueSkipFraction));
            return pursuingNear >= required;
        }

        /// <summary>
        /// Mục tiêu: Coi là đã tập hợp khi đủ unit đứng gần ô formation (giống player click một chỗ).
        /// Cách hoạt động: Tính lưới vuông quanh destination; đếm unit trong bán kính tới ô của mình.
        /// </summary>
        public static bool IsGatheredAtFormation(
            IReadOnlyList<AbstractUnit> units,
            Vector3 destination,
            float spacingMultiplier,
            float slotArrivalRadius,
            float requiredFraction)
        {
            if (units == null || units.Count == 0)
            {
                return false;
            }

            Vector3[] slots = UnitSquareFormationPlanner.ComputeWorldPositions(
                units,
                destination,
                spacingMultiplier);

            int arrived = 0;
            int considered = 0;
            float radiusSqr = slotArrivalRadius * slotArrivalRadius;

            for (int i = 0; i < units.Count && i < slots.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                considered++;
                Vector3 offset = unit.transform.position - slots[i];
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSqr)
                {
                    arrived++;
                }
            }

            if (considered == 0)
            {
                return false;
            }

            int required = Mathf.Max(1, Mathf.CeilToInt(considered * Mathf.Clamp01(requiredFraction)));
            return arrived >= required;
        }

        private static bool IsUnitPursuingFormationMove(
            AbstractUnit unit,
            Vector3 destination,
            float goalTolerance)
        {
            if (!unit.TryGetPlannerMoveGoal(out Vector3 goal))
            {
                return false;
            }

            if (unit.IsPursuingMoveGoalNear(destination, goalTolerance))
            {
                return true;
            }

            Vector3 toGoal = goal - unit.transform.position;
            toGoal.y = 0f;
            return toGoal.sqrMagnitude <= 4f;
        }

        private static float EstimateFormationFootprintRadius(IReadOnlyList<AbstractUnit> units)
        {
            int count = 0;
            float maxAgentRadius = 0.5f;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == null)
                {
                    continue;
                }

                count++;
                maxAgentRadius = Mathf.Max(maxAgentRadius, units[i].AgentRadius);
            }

            if (count <= 1)
            {
                return Mathf.Max(6f, maxAgentRadius * 4f);
            }

            float spacing = Mathf.Max(1.25f, maxAgentRadius * 2f * 3.5f);
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
            float halfWidth = (cols - 1) * 0.5f * spacing;
            float depth = rows * spacing;
            return Mathf.Max(10f, halfWidth + depth * 0.5f + spacing);
        }

        /// <summary>
        /// Mục tiêu: Chỉ 1 unit mới MoveTo trực tiếp lên target — nhóm dùng formation quanh hit.point.
        /// Cách hoạt động: Tránh mọi lính chồng lên cùng một transform (building/unit đang build).
        /// </summary>
        private static bool TryMoveToVisibleTarget(IReadOnlyList<AbstractUnit> units, RaycastHit hit)
        {
            if (hit.collider == null
                || !hit.collider.TryGetComponent(out AbstractCommandable commandable)
                || !commandable.IsVisible)
            {
                return false;
            }

            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                return false;
            }

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    units[i].MoveTo(commandable.transform);
                    return true;
                }
            }

            return false;
        }
    }
}
