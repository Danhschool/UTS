using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Điểm patrol ngẫu nhiên quanh Civil Central — quân rảnh scout/mở map.
    /// </summary>
    public static class AIMilitaryPatrolUtility
    {
        private const float NavMeshSampleDistance = 12f;
        private const float UnlimitedRadiusGrowthPerPhase = 8f;

        /// <summary>
        /// Mục tiêu: Đích patrol ngẫu nhiên (không theo kịch bản cố định theo instanceId).
        /// Cách hoạt động: Random bán kính + góc; patrolPhase chỉ mở rộng vùng khi MaxRadius = 0.
        /// </summary>
        public static bool TryGetRandomPatrolDestination(
            Vector3 civilCentralPosition,
            int patrolPhase,
            float minRadius,
            float maxRadius,
            out Vector3 destination)
        {
            destination = default;
            if (minRadius < 0f)
            {
                return false;
            }

            float upperRadius = GetCurrentPatrolUpperRadius(patrolPhase, minRadius, maxRadius);
            if (upperRadius < minRadius)
            {
                upperRadius = minRadius + 4f;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float radius = Random.Range(minRadius, upperRadius);
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 candidate = civilCentralPosition + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleDistance, NavMesh.AllAreas))
                {
                    destination = hit.position;
                    return true;
                }
            }

            if (NavMesh.SamplePosition(civilCentralPosition, out NavMeshHit fallbackHit, upperRadius, NavMesh.AllAreas))
            {
                destination = fallbackHit.position;
                return true;
            }

            return false;
        }

        public static float GetCurrentPatrolUpperRadius(int patrolPhase, float minRadius, float maxRadius) =>
            ResolveUpperRadius(minRadius, maxRadius, patrolPhase);

        private static float ResolveUpperRadius(float minRadius, float maxRadius, int patrolPhase)
        {
            if (maxRadius > minRadius)
            {
                return maxRadius;
            }

            return minRadius + 24f + patrolPhase * UnlimitedRadiusGrowthPerPhase;
        }

        public static bool IsEligibleForPatrol(AbstractUnit unit) =>
            unit != null && unit is not Worker && unit.IsAvailableForPlannerPatrol();
    }
}
