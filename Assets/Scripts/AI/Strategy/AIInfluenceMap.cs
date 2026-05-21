using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Lưới influence thô (XZ) — defense quanh Civil Central, threat từ vị trí địch.
    /// Dùng cho <see cref="AIBaseManager"/> chọn điểm đặt Corral/Forge an toàn.
    /// </summary>
    public sealed class AIInfluenceMap
    {
        private float[] safeScores = System.Array.Empty<float>();
        private int gridHalfWidth;
        private float cellSize;
        private Vector3 origin;
        private int gridWidth;

        public float CellSize => cellSize;
        public Vector3 Origin => origin;
        public int GridWidth => gridWidth;

        /// <summary>
        /// Mục tiêu: Tái tạo lưới influence cho tick hiện tại.
        /// Cách hoạt động: Mỗi cell = defense(CC) − threat(địch); clamp ≥ 0.
        /// </summary>
        public void Rebuild(
            Vector3 civilCentralPosition,
            IReadOnlyList<Vector3> threatPositions,
            AIInfluenceMapRuntimeConfig settings)
        {
            cellSize = Mathf.Max(4f, settings.CellSize);
            float radius = Mathf.Max(cellSize * 2f, settings.MapRadius);
            gridHalfWidth = Mathf.CeilToInt(radius / cellSize);
            gridWidth = gridHalfWidth * 2 + 1;
            int cellCount = gridWidth * gridWidth;

            if (safeScores.Length != cellCount)
            {
                safeScores = new float[cellCount];
            }

            origin = civilCentralPosition - new Vector3(gridHalfWidth * cellSize, 0f, gridHalfWidth * cellSize);

            float defenseRadius = Mathf.Max(cellSize, settings.DefenseRadius);
            float threatRadius = Mathf.Max(cellSize, settings.ThreatRadius);

            for (int z = 0; z < gridWidth; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector3 cellCenter = GetCellWorldCenter(x, z);
                    float defense = GaussianFalloff(
                        Vector3.Distance(cellCenter, civilCentralPosition),
                        defenseRadius,
                        settings.DefenseWeight);
                    float threat = 0f;

                    for (int t = 0; t < threatPositions.Count; t++)
                    {
                        threat += GaussianFalloff(
                            Vector3.Distance(cellCenter, threatPositions[t]),
                            threatRadius,
                            settings.ThreatWeight);
                    }

                    safeScores[z * gridWidth + x] = Mathf.Max(0f, defense - threat);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Điểm có influence an toàn cao nhất (tùy chọn ưu tiên gần CC cho Corral).
        /// Cách hoạt động: Duyệt cell; score = safe + bias gần/xa anchor.
        /// </summary>
        public bool TryGetBestCell(
            Vector3 anchorPosition,
            float preferNearAnchorWeight,
            out Vector3 worldPosition,
            out float bestScore)
        {
            worldPosition = default;
            bestScore = float.MinValue;
            if (safeScores.Length == 0)
            {
                return false;
            }

            for (int z = 0; z < gridWidth; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    float safe = safeScores[z * gridWidth + x];
                    if (safe <= 0f)
                    {
                        continue;
                    }

                    Vector3 cellCenter = GetCellWorldCenter(x, z);
                    float dist = Vector3.Distance(cellCenter, anchorPosition);
                    float score = safe - dist * preferNearAnchorWeight;
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    worldPosition = cellCenter;
                }
            }

            return bestScore > float.MinValue;
        }

        /// <summary>
        /// Mục tiêu: Thu thập vị trí địch trong bán kính map (một lần mỗi tick).
        /// Cách hoạt động: FindObjectsByType AbstractUnit; lọc owner khác + còn máu.
        /// </summary>
        public static void CollectThreatPositions(
            Owner friendlyOwner,
            Vector3 civilCentralPosition,
            float maxRadius,
            List<Vector3> output)
        {
            output.Clear();
            AbstractUnit[] allUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            float maxSqr = maxRadius * maxRadius;

            for (int i = 0; i < allUnits.Length; i++)
            {
                AbstractUnit unit = allUnits[i];
                if (unit == null
                    || unit.Owner == friendlyOwner
                    || unit.Owner == Owner.Invalid
                    || unit.Owner == Owner.Unowned
                    || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                float sqr = (unit.transform.position - civilCentralPosition).sqrMagnitude;
                if (sqr > maxSqr)
                {
                    continue;
                }

                output.Add(unit.transform.position);
            }
        }

        private Vector3 GetCellWorldCenter(int x, int z) =>
            origin + new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);

        private static float GaussianFalloff(float distance, float radius, float weight)
        {
            if (radius <= 0f || weight <= 0f)
            {
                return 0f;
            }

            float t = Mathf.Clamp01(1f - distance / radius);
            return weight * t * t;
        }
    }
}
