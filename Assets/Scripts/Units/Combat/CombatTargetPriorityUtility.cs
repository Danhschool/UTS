using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Ưu tiên mục tiêu — lính địch trước công trình địch (cùng khoảng cách / trong vùng).
    /// </summary>
    public static class CombatTargetPriorityUtility
    {
        /// <summary>
        /// Mục tiêu: Nhận diện nhà địch (gồm CC).
        /// Cách hoạt động: IDamageable → GetComponentInParent BaseBuilding; lọc owner và HP.
        /// </summary>
        public static bool TryGetHostileBuilding(
            IDamageable damageable,
            Owner friendlyOwner,
            bool requireVisible,
            out BaseBuilding building)
        {
            building = null;
            if (damageable == null || damageable.CurrentHealth <= 0)
            {
                return false;
            }

            Transform root = damageable.Transform;
            if (root == null)
            {
                return false;
            }

            building = root.GetComponentInParent<BaseBuilding>();
            if (building == null
                || building.Owner == friendlyOwner
                || building.Owner == Owner.Unowned
                || building.Owner == Owner.Invalid
                || building.CurrentHealth <= 0)
            {
                building = null;
                return false;
            }

            if (requireVisible && !FactionFogQuery.IsVisibleTo(friendlyOwner, building))
            {
                building = null;
                return false;
            }

            return true;
        }

        public static bool IsHostileBuildingTarget(
            IDamageable damageable,
            Owner friendlyOwner,
            bool requireVisible) =>
            TryGetHostileBuilding(damageable, friendlyOwner, requireVisible, out _);

        public static bool IsHostileUnitTarget(
            IDamageable damageable,
            Owner friendlyOwner,
            bool requireVisible)
        {
            if (damageable == null
                || damageable.CurrentHealth <= 0
                || IsHostileBuildingTarget(damageable, friendlyOwner, requireVisible))
            {
                return false;
            }

            if (damageable.Owner == friendlyOwner
                || damageable.Owner == Owner.Unowned
                || damageable.Owner == Owner.Invalid)
            {
                return false;
            }

            if (!requireVisible)
            {
                return true;
            }

            return damageable is IHideable hideable && FactionFogQuery.IsVisibleTo(friendlyOwner, hideable);
        }

        /// <summary>
        /// Mục tiêu: Sắp xếp NearbyEnemies — lính địch gần nhất trước, rồi nhà địch.
        /// </summary>
        public static void SortGameObjectsByUnitPriority(List<GameObject> targets, Vector3 fromPosition)
        {
            if (targets == null || targets.Count < 2)
            {
                return;
            }

            targets.Sort((a, b) => CompareGameObjectPriority(a, b, fromPosition));
        }

        /// <summary>
        /// Mục tiêu: Chọn threat — lính gần nhất, không có lính mới chọn công trình.
        /// </summary>
        public static bool TryPickPreferredThreat(
            Vector3 fromPosition,
            Owner friendlyOwner,
            bool requireVisible,
            IReadOnlyList<IDamageable> threats,
            out IDamageable best)
        {
            best = null;
            float bestBuildingSqr = float.MaxValue;
            float bestUnitSqr = float.MaxValue;
            IDamageable bestBuilding = null;
            IDamageable bestUnit = null;

            for (int i = 0; i < threats.Count; i++)
            {
                IDamageable threat = threats[i];
                if (threat?.Transform == null || threat.CurrentHealth <= 0)
                {
                    continue;
                }

                float sqr = (threat.Transform.position - fromPosition).sqrMagnitude;
                if (IsHostileBuildingTarget(threat, friendlyOwner, requireVisible))
                {
                    if (sqr < bestBuildingSqr)
                    {
                        bestBuildingSqr = sqr;
                        bestBuilding = threat;
                    }

                    continue;
                }

                if (IsHostileUnitTarget(threat, friendlyOwner, requireVisible)
                    && sqr < bestUnitSqr)
                {
                    bestUnitSqr = sqr;
                    bestUnit = threat;
                }
            }

            if (bestUnit != null)
            {
                best = bestUnit;
                return true;
            }

            if (bestBuilding != null)
            {
                best = bestBuilding;
                return true;
            }

            return false;
        }

        private static int CompareGameObjectPriority(GameObject a, GameObject b, Vector3 fromPosition)
        {
            bool aBuilding = a != null && a.GetComponentInParent<BaseBuilding>() != null;
            bool bBuilding = b != null && b.GetComponentInParent<BaseBuilding>() != null;
            if (aBuilding != bBuilding)
            {
                return aBuilding.CompareTo(bBuilding);
            }

            if (a == null || b == null)
            {
                return 0;
            }

            float aSqr = (a.transform.position - fromPosition).sqrMagnitude;
            float bSqr = (b.transform.position - fromPosition).sqrMagnitude;
            return aSqr.CompareTo(bSqr);
        }
    }
}
