using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phiên rally hai pha — lùi tập hợp rồi mới tấn công (theo Owner AI).
    /// </summary>
    public static class AIMilitaryRallySessionTracker
    {
        public enum RallyPhase
        {
            Idle,
            Regrouping,
            Attacking
        }

        private sealed class RallySession
        {
            public RallyPhase Phase;
            public Vector3 AttackPoint;
            public Vector3 RegroupPoint;
            public bool RegroupMoveIssued;
        }

        private static readonly Dictionary<int, RallySession> SessionsByOwner = new(4);
        private static readonly HashSet<int> RegroupCompletedOwners = new(4);

        /// <summary>
        /// Mục tiêu: Bắt đầu hoặc duy trì phiên rally khi còn thấy địch.
        /// Cách hoạt động: CC địch — lùi tập hợp đúng một lần rồi luôn Attacking; không reset khi mục tiêu lệch nhẹ.
        /// </summary>
        public static RallyPhase SyncHostileContact(
            Owner aiOwner,
            Vector3 attackPoint,
            Vector3 friendlyCivilCentralPosition,
            float stagingRadiusFromFriendlyCc,
            float maxFractionOfDistanceToThreat)
        {
            int key = (int)aiOwner;
            if (!SessionsByOwner.TryGetValue(key, out RallySession session))
            {
                session = new RallySession();
                SessionsByOwner[key] = session;
            }

            Vector3 previousAttackPoint = session.AttackPoint;
            session.AttackPoint = attackPoint;
            AIMilitaryRallyPlanner.TryComputeRegroupPoint(
                attackPoint,
                friendlyCivilCentralPosition,
                stagingRadiusFromFriendlyCc,
                maxFractionOfDistanceToThreat,
                out session.RegroupPoint);

            if (RegroupCompletedOwners.Contains(key))
            {
                session.Phase = RallyPhase.Attacking;
                return session.Phase;
            }

            bool targetMoved = session.Phase != RallyPhase.Idle
                && HorizontalDistance(previousAttackPoint, attackPoint) > 40f;

            if (session.Phase == RallyPhase.Idle
                || (targetMoved && !RegroupCompletedOwners.Contains(key)))
            {
                session.Phase = RallyPhase.Regrouping;
                session.RegroupMoveIssued = false;
            }

            return session.Phase;
        }

        public static void Clear(Owner aiOwner) => SessionsByOwner.Remove((int)aiOwner);

        public static bool ShouldRetainSessionOnLostContact(Owner aiOwner) =>
            SessionsByOwner.TryGetValue((int)aiOwner, out RallySession session)
            && (session.RegroupMoveIssued || session.Phase == RallyPhase.Attacking);

        public static bool HasCompletedRegroup(Owner aiOwner) =>
            RegroupCompletedOwners.Contains((int)aiOwner);

        /// <summary>
        /// Mục tiêu: Đánh dấu đã phát lệnh lùi tập hợp một lần (nhà chính địch).
        /// </summary>
        public static void MarkRegroupMoveIssued(Owner aiOwner)
        {
            if (!SessionsByOwner.TryGetValue((int)aiOwner, out RallySession session))
            {
                return;
            }

            session.RegroupMoveIssued = true;
        }

        public static bool TryGetRegroupPoint(Owner aiOwner, out Vector3 regroupPoint)
        {
            regroupPoint = default;
            if (!SessionsByOwner.TryGetValue((int)aiOwner, out RallySession session)
                || session.Phase != RallyPhase.Regrouping)
            {
                return false;
            }

            regroupPoint = session.RegroupPoint;
            return true;
        }

        public static bool TryGetAttackPoint(Owner aiOwner, out Vector3 attackPoint)
        {
            attackPoint = default;
            if (!SessionsByOwner.TryGetValue((int)aiOwner, out RallySession session))
            {
                return false;
            }

            attackPoint = session.AttackPoint;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Đủ quân đã tới vùng tập hợp sau bước lùi.
        /// Cách hoạt động: Đếm unit trong bán kính gather / tổng rally force ≥ ngưỡng %.
        /// </summary>
        public static bool IsRegroupComplete(
            IReadOnlyList<AbstractUnit> ralliedUnits,
            Vector3 regroupPoint,
            float gatherRadius,
            float requiredFraction,
            MoveCommand moveCommand = null)
        {
            if (ralliedUnits == null || ralliedUnits.Count == 0)
            {
                return false;
            }

            float fraction = Mathf.Clamp01(requiredFraction);
            if (moveCommand != null && ralliedUnits.Count > 1)
            {
                float slotRadius = Mathf.Max(4f, gatherRadius * 0.45f);
                if (GroupFormationMoveUtility.IsGatheredAtFormation(
                        ralliedUnits,
                        regroupPoint,
                        moveCommand.FormationSpacingMultiplier,
                        slotRadius,
                        fraction))
                {
                    return true;
                }
            }

            int nearRegroup = 0;
            float radiusSqr = gatherRadius * gatherRadius;
            for (int i = 0; i < ralliedUnits.Count; i++)
            {
                AbstractUnit unit = ralliedUnits[i];
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                if (HorizontalDistanceSq(unit.transform.position, regroupPoint) <= radiusSqr)
                {
                    nearRegroup++;
                }
            }

            int required = Mathf.Max(1, Mathf.CeilToInt(ralliedUnits.Count * fraction));
            return nearRegroup >= required;
        }

        public static void AdvanceToAttackPhase(Owner aiOwner)
        {
            int key = (int)aiOwner;
            if (!SessionsByOwner.TryGetValue(key, out RallySession session))
            {
                return;
            }

            session.Phase = RallyPhase.Attacking;
            RegroupCompletedOwners.Add(key);
        }

        public static RallyPhase GetPhase(Owner aiOwner) =>
            SessionsByOwner.TryGetValue((int)aiOwner, out RallySession session)
                ? session.Phase
                : RallyPhase.Idle;

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }
}
