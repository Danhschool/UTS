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
        /// Mục tiêu: Đa số unit đã có lệnh Move tới cùng vùng đích — không gửi lại.
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
                if (unit.IsPursuingMoveGoalNear(destination, goalMatchRadius))
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

        private static bool TryMoveToVisibleTarget(IReadOnlyList<AbstractUnit> units, RaycastHit hit)
        {
            if (hit.collider == null
                || !hit.collider.TryGetComponent(out AbstractCommandable commandable)
                || !commandable.IsVisible)
            {
                return false;
            }

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    units[i].MoveTo(commandable.transform);
                }
            }

            return true;
        }
    }
}
