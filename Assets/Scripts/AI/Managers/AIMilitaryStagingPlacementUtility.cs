using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Điều chỉnh điểm formation/staging — tránh chồng lên công trường đang xây.
    /// </summary>
    public static class AIMilitaryStagingPlacementUtility
    {
        private const float DefaultConstructionClearance = 16f;

        /// <summary>
        /// Mục tiêu: Đẩy điểm tập hợp ra xa foundation Building — quân không đè worker/build.
        /// Cách hoạt động: Vài vòng đẩy staging khỏi mọi nhà đang state Building.
        /// </summary>
        public static Vector3 AvoidActiveConstructionSites(
            AIWorldStateSnapshot snapshot,
            Vector3 stagingPoint,
            float clearanceRadius = DefaultConstructionClearance)
        {
            if (snapshot == null || snapshot.Buildings == null || snapshot.Buildings.Count == 0)
            {
                return stagingPoint;
            }

            Vector3 resolved = stagingPoint;
            float minSqr = clearanceRadius * clearanceRadius;
            for (int pass = 0; pass < 3; pass++)
            {
                bool adjusted = false;
                for (int i = 0; i < snapshot.Buildings.Count; i++)
                {
                    BaseBuilding building = snapshot.Buildings[i];
                    if (building == null
                        || building.Progress.State != BuildingProgress.BuildingState.Building)
                    {
                        continue;
                    }

                    Vector3 site = building.transform.position;
                    Vector3 offset = resolved - site;
                    offset.y = 0f;
                    if (offset.sqrMagnitude >= minSqr)
                    {
                        continue;
                    }

                    Vector3 pushDir = offset.sqrMagnitude < 0.25f
                        ? Vector3.forward
                        : offset.normalized;
                    resolved = site + pushDir * clearanceRadius;
                    resolved.y = stagingPoint.y;
                    adjusted = true;
                }

                if (!adjusted)
                {
                    break;
                }
            }

            return resolved;
        }
    }
}
