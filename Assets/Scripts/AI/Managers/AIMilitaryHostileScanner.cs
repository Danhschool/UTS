using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Combat;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Quét địch / Civil Central địch trong scene (một lần mỗi tick military) — không cache registry owner khác.
    /// </summary>
    public static class AIMilitaryHostileScanner
    {
        private static readonly List<IDamageable> ThreatScratch = new(32);

        /// <summary>
        /// Mục tiêu: Tìm Civil Central của phe địch (Player1).
        /// Cách hoạt động: FindObjectsByType BaseBuilding; lọc owner + <see cref="CivilCentralUtility"/>.
        /// </summary>
        public static bool TryFindEnemyCivilCentral(Owner enemyOwner, out BaseBuilding civilCentral) =>
            TryFindEnemyCivilCentral(enemyOwner, requireVisible: false, out civilCentral);

        /// <summary>
        /// Mục tiêu: Civil Central địch đang trong tầm nhìn (scout / fog fair).
        /// </summary>
        public static bool TryFindVisibleEnemyCivilCentral(
            Owner enemyOwner,
            bool requireVisible,
            out BaseBuilding civilCentral)
        {
            if (!TryFindEnemyCivilCentral(enemyOwner, requireVisible, out civilCentral))
            {
                return false;
            }

            return !requireVisible || civilCentral.IsVisible;
        }

        private static bool TryFindEnemyCivilCentral(
            Owner enemyOwner,
            bool requireVisible,
            out BaseBuilding civilCentral)
        {
            civilCentral = null;
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null
                    || building.Owner != enemyOwner
                    || building.CurrentHealth <= 0
                    || !CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                if (requireVisible && !building.IsVisible)
                {
                    continue;
                }

                civilCentral = building;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Nhà địch (không phải CC) gần nhất đang visible — mục tiêu phụ khi chưa thấy CC.
        /// </summary>
        public static bool TryFindClosestVisibleHostileBuilding(
            Owner friendlyOwner,
            Vector3 fromPosition,
            bool requireVisible,
            out BaseBuilding closest)
        {
            closest = null;
            float bestSqr = float.MaxValue;
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (!IsHostileBuilding(building, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqr = (building.transform.position - fromPosition).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                closest = building;
            }

            return closest != null;
        }

        /// <summary>
        /// Mục tiêu: Lính địch RTS gần nhất (không Worker ta, không WildAnimal).
        /// </summary>
        public static bool TryFindClosestVisibleHostileUnit(
            Owner friendlyOwner,
            Vector3 fromPosition,
            bool requireVisible,
            out AbstractUnit closest)
        {
            closest = null;
            float bestSqr = float.MaxValue;
            AbstractUnit[] allUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);

            for (int i = 0; i < allUnits.Length; i++)
            {
                AbstractUnit unit = allUnits[i];
                if (!IsRtsHostileCombatUnit(unit, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqr = (unit.transform.position - fromPosition).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                closest = unit;
            }

            return closest != null;
        }

        /// <summary>
        /// Mục tiêu: Địch trong bán kính phòng thủ quanh CC AI.
        /// Cách hoạt động: Quét AbstractUnit; lọc owner khác; tùy chọn fog visible.
        /// </summary>
        public static int CollectThreatsNearCivilCentral(
            Owner friendlyOwner,
            Vector3 civilCentralPosition,
            float radius,
            bool requireVisible,
            List<IDamageable> output)
        {
            output.Clear();
            float radiusSqr = radius * radius;

            BaseBuilding[] allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            for (int i = 0; i < allBuildings.Length; i++)
            {
                BaseBuilding building = allBuildings[i];
                if (!IsHostileBuildingIncludingCivilCentral(building, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqr = (building.transform.position - civilCentralPosition).sqrMagnitude;
                if (sqr <= radiusSqr)
                {
                    output.Add(building);
                }
            }

            AbstractUnit[] allUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                AbstractUnit unit = allUnits[i];
                if (!IsHostileUnit(unit, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqr = (unit.transform.position - civilCentralPosition).sqrMagnitude;
                if (sqr > radiusSqr)
                {
                    continue;
                }

                output.Add(unit);
            }

            return output.Count;
        }

        /// <summary>
        /// Mục tiêu: Ước lượng quân địch gần một điểm (ngưỡng attack wave).
        /// Cách hoạt động: Đếm unit không phải Worker thuộc enemyOwner trong bán kính.
        /// </summary>
        public static int EstimateEnemyArmyNear(Vector3 position, Owner enemyOwner, float radius)
        {
            int count = 0;
            float radiusSqr = radius * radius;
            AbstractUnit[] allUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);

            for (int i = 0; i < allUnits.Length; i++)
            {
                AbstractUnit unit = allUnits[i];
                if (unit == null
                    || unit is WildAnimal
                    || unit.Owner != enemyOwner
                    || unit is Worker
                    || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                if ((unit.transform.position - position).sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Mục tiêu: Chọn mục tiêu trong vùng — ưu tiên lính địch, rồi công trình gần nhất.
        /// </summary>
        public static bool TryPickClosestThreat(
            Vector3 fromPosition,
            IReadOnlyList<IDamageable> threats,
            out IDamageable closest) =>
            TryPickClosestThreat(fromPosition, Owner.Invalid, requireVisible: false, threats, out closest);

        public static bool TryPickClosestThreat(
            Vector3 fromPosition,
            Owner friendlyOwner,
            bool requireVisible,
            IReadOnlyList<IDamageable> threats,
            out IDamageable closest) =>
            CombatTargetPriorityUtility.TryPickPreferredThreat(
                fromPosition,
                friendlyOwner,
                requireVisible,
                threats,
                out closest);

        public static List<IDamageable> ThreatScratchList => ThreatScratch;

        /// <summary>
        /// Mục tiêu: Đối thủ RTS — không animal, không đồng minh.
        /// </summary>
        public static bool IsRtsHostileCombatUnit(
            AbstractUnit unit,
            Owner friendlyOwner,
            bool requireVisible) =>
            IsHostileUnit(unit, friendlyOwner, requireVisible);

        private static bool IsHostileUnit(AbstractUnit unit, Owner friendlyOwner, bool requireVisible)
        {
            if (unit == null
                || unit is WildAnimal
                || unit.Owner == friendlyOwner
                || unit.Owner == Owner.Unowned
                || unit.Owner == Owner.Invalid
                || unit.CurrentHealth <= 0)
            {
                return false;
            }

            if (requireVisible && unit is IHideable hideable && !hideable.IsVisible)
            {
                return false;
            }

            return true;
        }

        private static bool IsHostileBuilding(
            BaseBuilding building,
            Owner friendlyOwner,
            bool requireVisible) =>
            IsHostileBuildingIncludingCivilCentral(building, friendlyOwner, requireVisible)
            && !CivilCentralUtility.IsCivilCentral(building);

        private static bool IsHostileBuildingIncludingCivilCentral(
            BaseBuilding building,
            Owner friendlyOwner,
            bool requireVisible)
        {
            if (building == null
                || building.Owner == friendlyOwner
                || building.Owner == Owner.Unowned
                || building.Owner == Owner.Invalid
                || building.CurrentHealth <= 0)
            {
                return false;
            }

            if (requireVisible && !building.IsVisible)
            {
                return false;
            }

            return true;
        }
    }
}
