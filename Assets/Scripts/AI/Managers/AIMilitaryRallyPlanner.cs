using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phát hiện nhà chính / đối thủ RTS (không animal) và chọn điểm rally tấn công formation.
    /// </summary>
    public static class AIMilitaryRallyPlanner
    {
        public enum RallyTrigger
        {
            None,
            VisibleEnemyCivilCentral,
            VisibleHostileBuilding,
            VisibleHostileUnit
        }

        /// <summary>
        /// Mục tiêu: Có mục tiêu đủ điều kiện để gọi quân về tấn công formation (80% hoặc 100% tùy scout).
        /// Cách hoạt động: Ưu tiên CC địch visible → nhà địch → lính địch; bỏ <see cref="WildAnimal"/>.
        /// </summary>
        public static bool TryResolveRallyAttackTarget(
            Owner friendlyOwner,
            Owner primaryEnemyOwner,
            bool requireVisible,
            Vector3 friendlyCivilCentralPosition,
            out Vector3 attackPoint,
            out RallyTrigger trigger)
        {
            attackPoint = default;
            trigger = RallyTrigger.None;

            if (AIMilitaryHostileScanner.TryFindClosestVisibleHostileUnit(
                    friendlyOwner,
                    friendlyCivilCentralPosition,
                    requireVisible,
                    out AbstractUnit hostileUnit))
            {
                attackPoint = hostileUnit.transform.position;
                trigger = RallyTrigger.VisibleHostileUnit;
                return true;
            }

            if (AIMilitaryHostileScanner.TryFindVisibleEnemyCivilCentral(
                    primaryEnemyOwner,
                    requireVisible,
                    out BaseBuilding enemyCc))
            {
                AIMilitaryEnemyTracker.RememberVisibleEnemyCivilCentral(friendlyOwner, enemyCc);
                attackPoint = enemyCc.transform.position;
                trigger = RallyTrigger.VisibleEnemyCivilCentral;
                return true;
            }

            if (AIMilitaryHostileScanner.TryFindClosestVisibleHostileBuilding(
                    friendlyOwner,
                    friendlyCivilCentralPosition,
                    requireVisible,
                    out BaseBuilding hostileBuilding))
            {
                attackPoint = hostileBuilding.transform.position;
                trigger = RallyTrigger.VisibleHostileBuilding;
                return true;
            }

            return false;
        }

        public static int GetMinimumArmyToRally(RallyTrigger trigger, int defaultMinArmy, int minArmyOnHostileContact) =>
            trigger == RallyTrigger.None
                ? defaultMinArmy
                : Mathf.Max(1, minArmyOnHostileContact);

        private const float NavMeshSampleDistance = 16f;

        /// <summary>
        /// Mục tiêu: Điểm tập hợp gần nhà mình (xa chỗ nhìn thấy địch), không ngay vị trí contact.
        /// Cách hoạt động: Từ CC ta, tiến về hướng địch một đoạn stagingRadius (tối đa % khoảng cách tới địch).
        /// </summary>
        public static bool TryComputeRegroupPoint(
            Vector3 attackPoint,
            Vector3 friendlyCivilCentralPosition,
            float stagingRadiusFromFriendlyCc,
            float maxFractionOfDistanceToThreat,
            out Vector3 regroupPoint)
        {
            regroupPoint = default;
            if (stagingRadiusFromFriendlyCc <= 0f)
            {
                return false;
            }

            Vector3 toThreat = attackPoint - friendlyCivilCentralPosition;
            toThreat.y = 0f;
            float distanceToThreat = toThreat.magnitude;
            if (distanceToThreat < 0.5f)
            {
                toThreat = Vector3.forward;
                distanceToThreat = 1f;
            }
            else
            {
                toThreat /= distanceToThreat;
            }

            float capDist = distanceToThreat * Mathf.Clamp01(maxFractionOfDistanceToThreat);
            float stagingDist = Mathf.Min(Mathf.Max(12f, stagingRadiusFromFriendlyCc), capDist);
            Vector3 candidate = friendlyCivilCentralPosition + toThreat * stagingDist;
            candidate.y = friendlyCivilCentralPosition.y;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                regroupPoint = hit.position;
                return true;
            }

            if (NavMesh.SamplePosition(friendlyCivilCentralPosition, out hit, NavMeshSampleDistance * 2f, NavMesh.AllAreas))
            {
                regroupPoint = hit.position;
                return true;
            }

            regroupPoint = candidate;
            return true;
        }
    }
}
