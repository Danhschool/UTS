using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Giới hạn hoạt động quân trong đĩa quanh CC (vòng tháp ngoài + leash).
    /// </summary>
    public static class AIMilitaryOperationalLeash
    {
        /// <summary>
        /// Mục tiêu: Bán kính hoạt động = vòng đã mở khóa + leash (mặc định +100m), không theo slot tháp đang build.
        /// </summary>
        public static float GetOperationalRadius(AIMilitarySettings settings, Owner owner)
        {
            if (settings == null || !settings.EnableDefenseRingExpansion)
            {
                return float.MaxValue;
            }

            int unlockedRing = AIMilitaryDefenseRingPlanner.GetMaxUnlockedRingIndex(owner);
            return GetOperationalRadiusForRing(settings, unlockedRing);
        }

        public static float GetOperationalRadiusForRing(AIMilitarySettings settings, int ringIndex)
        {
            if (settings == null || !settings.EnableDefenseRingExpansion)
            {
                return float.MaxValue;
            }

            float ringRadius = AIMilitaryDefenseRingPlanner.GetRingRadius(settings, ringIndex);
            return ringRadius + settings.OperationalLeashBeyondOuterRing;
        }

        public static bool IsWithinOperationalZone(Vector3 worldPosition, Vector3 civilCentralPosition, float operationalRadius)
        {
            if (operationalRadius >= float.MaxValue * 0.5f)
            {
                return true;
            }

            return HorizontalDistanceSq(worldPosition, civilCentralPosition) <= operationalRadius * operationalRadius;
        }

        /// <summary>
        /// Mục tiêu: Chỉ tổng tấn công khi CC địch nằm trong vùng hoạt động.
        /// </summary>
        public static bool IsEnemyCivilCentralInOperationalZone(
            Owner enemyOwner,
            Vector3 friendlyCivilCentralPosition,
            float operationalRadius,
            bool requireVisible)
        {
            if (!AIMilitaryHostileScanner.TryFindEnemyCivilCentral(enemyOwner, out BaseBuilding enemyCc))
            {
                return false;
            }

            if (requireVisible && !enemyCc.IsVisible)
            {
                return false;
            }

            return IsWithinOperationalZone(
                enemyCc.transform.position,
                friendlyCivilCentralPosition,
                operationalRadius);
        }

        public static int CountAliveMilitary(AIWorldStateSnapshot snapshot)
        {
            if (snapshot?.MilitaryUnits == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < snapshot.MilitaryUnits.Count; i++)
            {
                AbstractUnit unit = snapshot.MilitaryUnits[i];
                if (unit != null && unit.CurrentHealth > 0 && unit is not Worker)
                {
                    count++;
                }
            }

            return count;
        }

        private static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }
}
