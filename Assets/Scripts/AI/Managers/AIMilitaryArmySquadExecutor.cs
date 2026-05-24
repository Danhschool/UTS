using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Một đội quân — Attack cả nhóm lên mục tiêu chung, rồi formation Move (không một điểm chồng).
    /// </summary>
    public static class AIMilitaryArmySquadExecutor
    {
        /// <summary>
        /// Mục tiêu: Gom lính cho đội (chunk 10 hoặc toàn bộ quân khi tấn công tổng).
        /// Cách hoạt động: Duyện militaryScratch; bỏ unit đã assign tick.
        /// </summary>
        public static int CollectArmy(
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> excludeUnitIds,
            int maxChunkSize,
            bool useEntireArmy,
            List<AbstractUnit> armyOut)
        {
            armyOut.Clear();
            if (militaryUnits == null || militaryUnits.Count == 0)
            {
                return 0;
            }

            int limit = useEntireArmy ? militaryUnits.Count : Mathf.Max(1, maxChunkSize);
            for (int i = 0; i < militaryUnits.Count && armyOut.Count < limit; i++)
            {
                AbstractUnit unit = militaryUnits[i];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || excludeUnitIds != null && excludeUnitIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (!useEntireArmy)
                {
                    if (!AIMilitaryPatrolUtility.IsEligibleForPatrol(unit))
                    {
                        continue;
                    }
                }
                else if (unit is Worker)
                {
                    continue;
                }

                armyOut.Add(unit);
            }

            return armyOut.Count;
        }

        /// <summary>
        /// Mục tiêu: Gom lính cho điểm tập hợp — gồm cả unit đang Move (lính mới spawn vào đội ngay).
        /// Cách hoạt động: Bỏ worker/đã assign tick; không lọc IsAvailableForPlannerPatrol.
        /// </summary>
        public static int CollectArmyForStaging(
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> excludeUnitIds,
            int maxChunkSize,
            List<AbstractUnit> armyOut)
        {
            armyOut.Clear();
            if (militaryUnits == null || militaryUnits.Count == 0 || maxChunkSize <= 0)
            {
                return 0;
            }

            int limit = Mathf.Max(1, maxChunkSize);
            for (int i = 0; i < militaryUnits.Count && armyOut.Count < limit; i++)
            {
                AbstractUnit unit = militaryUnits[i];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || unit is Worker
                    || excludeUnitIds != null && excludeUnitIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                armyOut.Add(unit);
            }

            return armyOut.Count;
        }

        /// <summary>
        /// Mục tiêu: Gom tối đa squadSize lính cho staging/scout — chưa đủ 10 vẫn partial.
        /// Cách hoạt động: <see cref="CollectArmyForStaging"/>; true nếu có ít nhất một lính.
        /// </summary>
        public static bool TryCollectStagingSquad(
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> excludeUnitIds,
            int squadSize,
            List<AbstractUnit> squadOut)
        {
            squadOut.Clear();
            if (squadSize <= 0 || militaryUnits == null)
            {
                return false;
            }

            CollectArmyForStaging(militaryUnits, excludeUnitIds, squadSize, squadOut);
            return squadOut.Count > 0;
        }

        /// <summary>
        /// Mục tiêu: Đủ một đội đầy (ví dụ 10) trước scout/tấn công ngoài staging.
        /// </summary>
        public static bool TryCollectFullSquad(
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> excludeUnitIds,
            int squadSize,
            List<AbstractUnit> squadOut)
        {
            if (!TryCollectStagingSquad(militaryUnits, excludeUnitIds, squadSize, squadOut))
            {
                return false;
            }

            return squadOut.Count >= squadSize;
        }

        /// <summary>
        /// Mục tiêu: Cả đội Attack mục tiêu chung (CC/nhà) rồi formation tiến — giống multi-select + attack click.
        /// Cách hoạt động: Ưu tiên group Attack shared target; fallback per-unit attack; cuối cùng formation Move.
        /// </summary>
        public static bool TryExecuteSquadAttackAndAdvance(
            AIWorldStateSnapshot snapshot,
            Owner enemyOwner,
            bool requireVisible,
            IReadOnlyList<AbstractUnit> army,
            Vector3 advancePoint,
            AIMilitaryRallyPlanner.RallyTrigger trigger,
            MoveCommand moveCommand,
            List<AbstractUnit> moveFallbackScratch,
            float formationGoalRadius = 32f)
        {
            if (snapshot == null || army == null || army.Count == 0)
            {
                return false;
            }

            moveFallbackScratch?.Clear();

            if (TryExecuteGroupAttackOnClosestHostileUnit(
                    snapshot.Owner,
                    army,
                    advancePoint,
                    requireVisible))
            {
                return true;
            }

            if (TryExecuteGroupAttackOnClosestHostileBuilding(
                    snapshot.Owner,
                    army,
                    advancePoint,
                    requireVisible))
            {
                return true;
            }

            if (TryExecuteGroupAttackOnEnemyCivilCentral(
                    snapshot.Owner,
                    enemyOwner,
                    requireVisible,
                    army))
            {
                return true;
            }

            return AIMilitaryRallyCombatPlanner.TryExecuteAttackPhase(
                army,
                snapshot.Owner,
                enemyOwner,
                requireVisible,
                advancePoint,
                trigger,
                moveCommand,
                moveFallbackScratch)
                || TryFormationAdvance(army, advancePoint, moveCommand, formationGoalRadius);
        }

        /// <summary>
        /// Mục tiêu: Tổng lực sau buffer — Attack mục tiêu địch gần nhất (từ CC ta), formation tiến về CC địch.
        /// Cách hoạt động: Tìm unit/building gần friendly CC; group Attack; Move formation tới enemy CC.
        /// </summary>
        public static bool TryExecutePostContactOffensive(
            Owner friendlyOwner,
            Owner enemyOwner,
            bool requireVisible,
            Vector3 friendlyCivilCentralPosition,
            Vector3 enemyCivilCentralPoint,
            IReadOnlyList<AbstractUnit> army,
            MoveCommand moveCommand,
            List<AbstractUnit> moveFallbackScratch,
            float formationGoalRadius = 40f)
        {
            if (army == null || army.Count == 0 || moveCommand == null || enemyCivilCentralPoint == default)
            {
                return false;
            }

            moveFallbackScratch?.Clear();
            bool attacked = TryExecuteGroupAttackOnClosestHostileUnit(
                    friendlyOwner,
                    army,
                    friendlyCivilCentralPosition,
                    requireVisible)
                || TryExecuteGroupAttackOnClosestHostileBuilding(
                    friendlyOwner,
                    army,
                    friendlyCivilCentralPosition,
                    requireVisible);

            if (AIMilitaryHostileScanner.TryFindVisibleEnemyCivilCentral(
                    friendlyOwner,
                    enemyOwner,
                    requireVisible,
                    out BaseBuilding enemyCc))
            {
                attacked = TryExecuteGroupAttackOnBuilding(friendlyOwner, army, enemyCc) || attacked;
            }

            return attacked
                || TryFormationAdvance(army, enemyCivilCentralPoint, moveCommand, formationGoalRadius);
        }

        /// <summary>
        /// Mục tiêu: Đội tập hợp tại điểm staging (formation) trước scout/tấn công.
        /// </summary>
        public static bool TryFormationAssemble(
            IReadOnlyList<AbstractUnit> army,
            Vector3 stagingPoint,
            MoveCommand moveCommand,
            float goalMatchRadius = 28f,
            bool forceReissue = false)
        {
            if (army == null || army.Count == 0 || moveCommand == null)
            {
                return false;
            }

            RaycastHit hit = AIHitUtility.AtPoint(stagingPoint);
            if (forceReissue)
            {
                return GroupFormationMoveUtility.TryApplyMove(army, hit, moveCommand);
            }

            return GroupFormationMoveUtility.TryApplyMoveIfNeeded(army, hit, moveCommand, goalMatchRadius);
        }

        public static bool IsArmyGatheredAtStaging(
            IReadOnlyList<AbstractUnit> army,
            Vector3 stagingPoint,
            MoveCommand moveCommand,
            float gatherRadius,
            float requiredFraction) =>
            army != null
            && army.Count > 0
            && moveCommand != null
            && GroupFormationMoveUtility.IsGatheredAtFormation(
                army,
                stagingPoint,
                moveCommand.FormationSpacingMultiplier,
                Mathf.Max(4f, gatherRadius * 0.45f),
                requiredFraction);

        private static bool TryFormationAdvance(
            IReadOnlyList<AbstractUnit> army,
            Vector3 advancePoint,
            MoveCommand moveCommand,
            float goalMatchRadius)
        {
            if (moveCommand == null)
            {
                return false;
            }

            RaycastHit hit = AIHitUtility.AtPoint(advancePoint);
            return GroupFormationMoveUtility.TryApplyMoveIfNeeded(army, hit, moveCommand, goalMatchRadius);
        }

        /// <summary>
        /// Mục tiêu: Mọi lính trong đội Attack cùng Civil Central địch visible.
        /// </summary>
        private static bool TryExecuteGroupAttackOnEnemyCivilCentral(
            Owner friendlyOwner,
            Owner enemyOwner,
            bool requireVisible,
            IReadOnlyList<AbstractUnit> army)
        {
            if (!AIMilitaryHostileScanner.TryFindVisibleEnemyCivilCentral(
                    friendlyOwner,
                    enemyOwner,
                    requireVisible,
                    out BaseBuilding enemyCc))
            {
                return false;
            }

            return TryExecuteGroupAttackOnBuilding(friendlyOwner, army, enemyCc);
        }

        private static bool TryExecuteGroupAttackOnClosestHostileBuilding(
            Owner friendlyOwner,
            IReadOnlyList<AbstractUnit> army,
            Vector3 nearPoint,
            bool requireVisible)
        {
            if (!AIMilitaryHostileScanner.TryFindClosestVisibleHostileBuilding(
                    friendlyOwner,
                    nearPoint,
                    requireVisible,
                    out BaseBuilding building))
            {
                return false;
            }

            return TryExecuteGroupAttackOnBuilding(friendlyOwner, army, building);
        }

        /// <summary>
        /// Mục tiêu: Cả đội Attack cùng một lính địch RTS visible gần điểm tiến công.
        /// </summary>
        private static bool TryExecuteGroupAttackOnClosestHostileUnit(
            Owner friendlyOwner,
            IReadOnlyList<AbstractUnit> army,
            Vector3 nearPoint,
            bool requireVisible)
        {
            if (!AIMilitaryHostileScanner.TryFindClosestVisibleHostileUnit(
                    friendlyOwner,
                    nearPoint,
                    requireVisible,
                    out AbstractUnit hostile))
            {
                return false;
            }

            Collider collider = hostile.GetComponent<Collider>()
                ?? hostile.GetComponentInChildren<Collider>();
            if (collider == null || !collider.TryGetComponent(out IDamageable damageable))
            {
                return false;
            }

            return TryExecuteGroupAttackOnDamageable(friendlyOwner, army, damageable, collider);
        }

        /// <summary>
        /// Mục tiêu: Gửi AttackCommand.Handle cho từng lính trong đội lên cùng một building.
        /// </summary>
        private static bool TryExecuteGroupAttackOnBuilding(
            Owner friendlyOwner,
            IReadOnlyList<AbstractUnit> army,
            BaseBuilding building)
        {
            if (building == null || building.CurrentHealth <= 0)
            {
                return false;
            }

            Collider collider = building.GetComponent<Collider>()
                ?? building.GetComponentInChildren<Collider>();
            if (collider == null || !collider.TryGetComponent(out IDamageable damageable))
            {
                return false;
            }

            return TryExecuteGroupAttackOnDamageable(friendlyOwner, army, damageable, collider);
        }

        /// <summary>
        /// Mục tiêu: Mọi lính trong đội nhận Attack cùng một <see cref="IDamageable"/>.
        /// </summary>
        private static bool TryExecuteGroupAttackOnDamageable(
            Owner friendlyOwner,
            IReadOnlyList<AbstractUnit> army,
            IDamageable damageable,
            Collider collider)
        {
            if (damageable == null || damageable.Owner == friendlyOwner)
            {
                return false;
            }

            int issued = 0;
            for (int i = 0; i < army.Count; i++)
            {
                AbstractUnit unit = army[i];
                if (unit != null
                    && AIMilitaryRallyCombatPlanner.TryApplyAttackOnDamageable(
                        unit,
                        friendlyOwner,
                        damageable,
                        collider))
                {
                    issued++;
                }
            }

            return issued > 0;
        }
    }
}
