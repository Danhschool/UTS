using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phase Attacking — gán Attack lên địch visible; Move formation chỉ là fallback.
    /// </summary>
    public static class AIMilitaryRallyCombatPlanner
    {
        /// <summary>
        /// Mục tiêu: Sau tập hợp xa — chuyển quân sang Command Attack (ưu tiên lính địch trong tầm nhìn).
        /// Cách hoạt động: Mỗi unit tìm mục tiêu RTS visible; không có target thì formation Move về attackPoint.
        /// </summary>
        public static bool TryExecuteAttackPhase(
            IReadOnlyList<AbstractUnit> army,
            Owner friendlyOwner,
            Owner primaryEnemyOwner,
            bool requireVisible,
            Vector3 attackPoint,
            AIMilitaryRallyPlanner.RallyTrigger trigger,
            MoveCommand moveCommand,
            List<AbstractUnit> moveFallbackScratch)
        {
            if (army == null || army.Count == 0)
            {
                return false;
            }

            moveFallbackScratch?.Clear();
            int attackOrders = 0;

            for (int i = 0; i < army.Count; i++)
            {
                AbstractUnit unit = army[i];
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                if (TryOrderUnitAttack(
                        unit,
                        friendlyOwner,
                        primaryEnemyOwner,
                        requireVisible,
                        attackPoint,
                        trigger))
                {
                    attackOrders++;
                    continue;
                }

                moveFallbackScratch?.Add(unit);
            }

            if (moveFallbackScratch != null
                && moveFallbackScratch.Count > 0
                && moveCommand != null)
            {
                RaycastHit hit = AIHitUtility.AtPoint(attackPoint);
                GroupFormationMoveUtility.TryApplyMoveIfNeeded(moveFallbackScratch, hit, moveCommand, 28f);
            }

            return attackOrders > 0 || (moveFallbackScratch != null && moveFallbackScratch.Count > 0);
        }

        /// <summary>
        /// Mục tiêu: Một lính nhận Attack — ưu tiên lính địch gần nhất, rồi CC/nhà visible.
        /// </summary>
        private static bool TryOrderUnitAttack(
            AbstractUnit unit,
            Owner friendlyOwner,
            Owner primaryEnemyOwner,
            bool requireVisible,
            Vector3 attackPoint,
            AIMilitaryRallyPlanner.RallyTrigger trigger)
        {
            if (trigger == AIMilitaryRallyPlanner.RallyTrigger.VisibleHostileUnit
                && AIMilitaryHostileScanner.TryFindClosestVisibleHostileUnit(
                    friendlyOwner,
                    unit.transform.position,
                    requireVisible,
                    out AbstractUnit hostileUnit)
                && TryApplyAttackOnUnit(unit, friendlyOwner, hostileUnit))
            {
                return true;
            }

            if (AIMilitaryHostileScanner.TryFindClosestVisibleHostileUnit(
                    friendlyOwner,
                    unit.transform.position,
                    requireVisible,
                    out hostileUnit)
                && TryApplyAttackOnUnit(unit, friendlyOwner, hostileUnit))
            {
                return true;
            }

            if (AIMilitaryHostileScanner.TryFindVisibleEnemyCivilCentral(
                    primaryEnemyOwner,
                    requireVisible,
                    out BaseBuilding enemyCc)
                && TryApplyAttackOnBuilding(unit, friendlyOwner, enemyCc))
            {
                return true;
            }

            if (AIMilitaryHostileScanner.TryFindClosestVisibleHostileBuilding(
                    friendlyOwner,
                    unit.transform.position,
                    requireVisible,
                    out BaseBuilding hostileBuilding)
                && TryApplyAttackOnBuilding(unit, friendlyOwner, hostileBuilding))
            {
                return true;
            }

            return false;
        }

        private static bool TryApplyAttackOnUnit(
            AbstractUnit attacker,
            Owner friendlyOwner,
            AbstractUnit hostile)
        {
            if (hostile == null || hostile.CurrentHealth <= 0)
            {
                return false;
            }

            Collider collider = hostile.GetComponent<Collider>()
                ?? hostile.GetComponentInChildren<Collider>();
            if (collider == null || !collider.TryGetComponent(out IDamageable damageable))
            {
                return false;
            }

            return TryApplyAttackWithCommand(attacker, friendlyOwner, damageable, collider);
        }

        private static bool TryApplyAttackOnBuilding(
            AbstractUnit attacker,
            Owner friendlyOwner,
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

            return TryApplyAttackWithCommand(attacker, friendlyOwner, damageable, collider);
        }

        private static bool TryApplyAttackWithCommand(
            AbstractUnit attacker,
            Owner friendlyOwner,
            IDamageable damageable,
            Collider collider)
        {
            AttackCommand attack = ResolveAttackCommand(attacker);
            if (attack == null || damageable.Owner == friendlyOwner)
            {
                return false;
            }

            if (!AIHitUtility.TryCreateHit(collider, out RaycastHit hit))
            {
                return false;
            }

            CommandContext context = new(friendlyOwner, attacker, hit, mouseButton: MouseButton.Right);
            if (!attack.CanHandle(context))
            {
                return false;
            }

            attack.Handle(context);
            return true;
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
    }
}
