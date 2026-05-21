using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Tự tính tỷ lệ influence (cell, bán kính, weight) từ footprint nhà và phạm vi đặt base.
    /// </summary>
    public static class AIInfluenceMapConfigResolver
    {
        private const float MinCellSize = 8f;
        private const float MaxCellSize = 16f;
        private const float DefenseWeight = 4f;
        private const float ThreatWeight = 3f;

        /// <summary>
        /// Mục tiêu: Thông số influence khớp map/prefab thay vì hardcode 12m/52m.
        /// Cách hoạt động: Footprint Corral/Forge + placement reach + spread building quanh CC.
        /// </summary>
        public static AIInfluenceMapRuntimeConfig Resolve(
            AIWorldStateSnapshot snapshot,
            AIBaseRuntimeConfig baseConfig)
        {
            float footprint = baseConfig.MaxPlacementFootprint;
            float placementReach = baseConfig.PlacementSearchRings * baseConfig.PlacementSearchStep;

            float cellSize = Mathf.Clamp(footprint * 1.05f, MinCellSize, MaxCellSize);
            float baseSpread = ComputeBaseSpreadFromCc(snapshot);
            float mapRadius = Mathf.Clamp(
                Mathf.Max(placementReach * 1.45f, baseSpread * 0.55f + cellSize * 3f),
                28f,
                88f);
            float defenseRadius = Mathf.Clamp(mapRadius * 0.68f, cellSize * 2.5f, mapRadius);
            float threatRadius = Mathf.Clamp(cellSize * 1.85f, 12f, defenseRadius * 0.62f);

            return new AIInfluenceMapRuntimeConfig(
                cellSize,
                mapRadius,
                defenseRadius,
                threatRadius,
                DefenseWeight,
                ThreatWeight);
        }

        /// <summary>
        /// Mục tiêu: Biên vùng “trong base” theo nhà AI đã có (không chỉ hằng số).
        /// Cách hoạt động: Khoảng cách xa nhất từ CC tới building hoàn thành + margin placement.
        /// </summary>
        private static float ComputeBaseSpreadFromCc(AIWorldStateSnapshot snapshot)
        {
            if (snapshot.CivilCentral == null)
            {
                return 24f;
            }

            Vector3 cc = snapshot.CivilCentral.transform.position;
            float maxDist = 0f;

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building == null
                    || building == snapshot.CivilCentral
                    || building.Progress.State != BuildingProgress.BuildingState.Completed)
                {
                    continue;
                }

                float dist = Vector3.Distance(building.transform.position, cc);
                if (dist > maxDist)
                {
                    maxDist = dist;
                }
            }

            return Mathf.Max(maxDist, 16f);
        }
    }

    /// <summary>Tham số influence đã resolve — read-only, không cần Inspector.</summary>
    public readonly struct AIInfluenceMapRuntimeConfig
    {
        public float CellSize { get; }
        public float MapRadius { get; }
        public float DefenseRadius { get; }
        public float ThreatRadius { get; }
        public float DefenseWeight { get; }
        public float ThreatWeight { get; }

        public AIInfluenceMapRuntimeConfig(
            float cellSize,
            float mapRadius,
            float defenseRadius,
            float threatRadius,
            float defenseWeight,
            float threatWeight)
        {
            CellSize = cellSize;
            MapRadius = mapRadius;
            DefenseRadius = defenseRadius;
            ThreatRadius = threatRadius;
            DefenseWeight = defenseWeight;
            ThreatWeight = threatWeight;
        }
    }
}
