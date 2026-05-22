using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Tìm mục tiêu hostiles trong vùng tròn (rampage / sweep).
    /// </summary>
    public static class CombatAreaTargetQuery
    {
        private static readonly Collider[] OverlapScratch = new Collider[96];

        /// <summary>
        /// Mục tiêu: Hostile trong bán kính — ưu tiên lính địch, rồi công trình.
        /// Cách hoạt động: OverlapSphere; tách unit/building; chọn gần nhất từng nhóm.
        /// </summary>
        public static bool TryFindClosestHostileInRadius(
            AbstractUnit searcher,
            Vector3 areaCenter,
            float radius,
            IDamageable exclude,
            out IDamageable hostile)
        {
            hostile = null;
            if (searcher == null || radius <= 0f)
            {
                return false;
            }

            AttackConfigSO attackConfig = searcher.UnitSO is UnitSO unitData ? unitData.AttackConfig : null;
            if (attackConfig == null)
            {
                return false;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                areaCenter,
                radius,
                OverlapScratch,
                attackConfig.DamageableLayers,
                QueryTriggerInteraction.Collide);

            Owner friendlyOwner = searcher.Owner;
            Vector3 from = searcher.transform.position;
            float bestBuildingSqr = float.MaxValue;
            float bestUnitSqr = float.MaxValue;
            IDamageable bestBuilding = null;
            IDamageable bestUnit = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = OverlapScratch[i];
                if (collider == null
                    || !collider.TryGetComponent(out IDamageable damageable)
                    || damageable == exclude
                    || damageable.Owner == friendlyOwner
                    || damageable.CurrentHealth <= 0
                    || !IsTargetVisible(damageable))
                {
                    continue;
                }

                Transform targetTransform = damageable.Transform;
                if (targetTransform == null)
                {
                    continue;
                }

                float sqr = (targetTransform.position - from).sqrMagnitude;
                if (CombatTargetPriorityUtility.IsHostileBuildingTarget(damageable, friendlyOwner, requireVisible: true))
                {
                    if (sqr < bestBuildingSqr)
                    {
                        bestBuildingSqr = sqr;
                        bestBuilding = damageable;
                    }

                    continue;
                }

                if (CombatTargetPriorityUtility.IsHostileUnitTarget(damageable, friendlyOwner, requireVisible: true)
                    && sqr < bestUnitSqr)
                {
                    bestUnitSqr = sqr;
                    bestUnit = damageable;
                }
            }

            if (bestUnit != null)
            {
                hostile = bestUnit;
                return true;
            }

            if (bestBuilding != null)
            {
                hostile = bestBuilding;
                return true;
            }

            return false;
        }

        private static bool IsTargetVisible(IDamageable damageable)
        {
            if (damageable is IHideable hideable)
            {
                return hideable.IsVisible;
            }

            return true;
        }
    }
}
