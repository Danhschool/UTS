using GameDevTV.RTS.Minimap;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Đánh giá vùng patrol đã được khám phá đủ chưa (bán kính patrol + fog explored).
    /// </summary>
    public static class AIMilitaryMapExplorationUtility
    {
        private const int DefaultCoverageGrid = 8;

        /// <summary>
        /// Mục tiêu: Biết khi nào AI được gọi 100% quân đi đánh thay vì giữ ~20% scout.
        /// Cách hoạt động: Đạt patrol max radius HOẶC tỷ lệ ô explored trong đĩa patrol ≥ ngưỡng fog.
        /// </summary>
        public static bool IsExplorationComplete(
            int patrolPhase,
            float patrolMinRadius,
            float patrolMaxRadius,
            float patrolRingStep,
            float exploredCoverageThreshold,
            Vector3 civilCentralPosition,
            MinimapFogSystemReference fogSystem)
        {
            float upperRadius = AIMilitaryPatrolUtility.GetCurrentPatrolUpperRadius(
                patrolPhase,
                patrolMinRadius,
                patrolMaxRadius,
                patrolRingStep);

            if (patrolMaxRadius > patrolMinRadius
                && upperRadius >= patrolMaxRadius - 0.5f)
            {
                return true;
            }

            if (fogSystem == null
                || !fogSystem.IsFogReady
                || exploredCoverageThreshold <= 0f)
            {
                return false;
            }

            float coverage = EstimateExploredCoverageInDisk(
                fogSystem,
                civilCentralPosition,
                upperRadius,
                DefaultCoverageGrid);
            return coverage >= exploredCoverageThreshold;
        }

        /// <summary>
        /// Mục tiêu: Ước lượng % map đã explore trong vòng patrol hiện tại.
        /// Cách hoạt động: Lưới điểm trong đĩa; đếm <see cref="MinimapFogSystemReference.IsWorldPositionExplored"/>.
        /// </summary>
        public static float EstimateExploredCoverageInDisk(
            MinimapFogSystemReference fogSystem,
            Vector3 center,
            float radius,
            int gridResolution = DefaultCoverageGrid)
        {
            if (fogSystem == null || !fogSystem.IsFogReady || radius <= 0f || gridResolution < 2)
            {
                return 0f;
            }

            int explored = 0;
            int total = 0;
            float radiusSqr = radius * radius;
            float step = 2f / (gridResolution - 1);

            for (int gx = 0; gx < gridResolution; gx++)
            {
                float nx = -1f + gx * step;
                for (int gz = 0; gz < gridResolution; gz++)
                {
                    float nz = -1f + gz * step;
                    Vector3 offset = new Vector3(nx * radius, 0f, nz * radius);
                    if (offset.sqrMagnitude > radiusSqr)
                    {
                        continue;
                    }

                    Vector3 sample = center + offset;
                    total++;
                    if (fogSystem.IsWorldPositionExplored(sample))
                    {
                        explored++;
                    }
                }
            }

            return total > 0 ? explored / (float)total : 0f;
        }
    }
}
