using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Lưới influence thô (XZ) — defense quanh Civil Central, threat địch, economic cụm mỏ xa.
    /// Dùng cho <see cref="AIBaseManager"/> / <see cref="AIEconomyManager"/> / <see cref="AIMilitaryManager"/>.
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
        /// Cách hoạt động: Mỗi cell = defense(CC) − threat(địch) + economic(mỏ xa); clamp ≥ 0.
        /// </summary>
        public void Rebuild(
            Vector3 civilCentralPosition,
            IReadOnlyList<Vector3> threatPositions,
            IReadOnlyList<Vector3> economicPositions,
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
            float economicRadius = Mathf.Max(cellSize, settings.EconomicRadius);

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

                    float economic = 0f;
                    for (int e = 0; e < economicPositions.Count; e++)
                    {
                        economic += GaussianFalloff(
                            Vector3.Distance(cellCenter, economicPositions[e]),
                            economicRadius,
                            settings.EconomicWeight);
                    }

                    safeScores[z * gridWidth + x] = Mathf.Max(0f, defense - threat + economic);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Điểm có influence an toàn cao nhất (tùy chọn ưu tiên gần/xa anchor).
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
        /// Mục tiêu: Thu thập vị trí địch trong bán kính map từ snapshot (không FindObjectsByType).
        /// Cách hoạt động: Duyệt <see cref="AIWorldStateSnapshot.Units"/>; lọc hostile combat unit.
        /// </summary>
        public static void CollectThreatPositionsFromSnapshot(
            AIWorldStateSnapshot snapshot,
            Owner friendlyOwner,
            float maxRadius,
            bool requireVisible,
            List<Vector3> output)
        {
            output.Clear();
            if (snapshot?.CivilCentral == null)
            {
                return;
            }

            Vector3 ccPosition = snapshot.CivilCentral.transform.position;
            float maxSqr = maxRadius * maxRadius;

            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                AbstractUnit unit = snapshot.Units[i];
                if (!AIMilitaryHostileScanner.IsRtsHostileCombatUnit(unit, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqr = (unit.transform.position - ccPosition).sqrMagnitude;
                if (sqr > maxSqr)
                {
                    continue;
                }

                output.Add(unit.transform.position);
            }
        }

        /// <summary>
        /// Mục tiêu: Cụm mỏ xa Civil Central — tăng economic influence (ưu tiên Store).
        /// Cách hoạt động: Lọc <see cref="GatherableSupply"/> visible, cách CC &gt; ngưỡng.
        /// </summary>
        public static void CollectRemoteEconomicPositionsFromSnapshot(
            AIWorldStateSnapshot snapshot,
            Owner friendlyOwner,
            Vector3 civilCentralPosition,
            float remoteClusterMinDistance,
            List<Vector3> output)
        {
            output.Clear();
            if (snapshot == null || remoteClusterMinDistance >= float.MaxValue * 0.5f)
            {
                return;
            }

            float minSqr = remoteClusterMinDistance * remoteClusterMinDistance;
            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply supply = snapshot.GatherableSupplies[i];
                if (supply == null
                    || supply.Amount <= 0
                    || !FactionFogQuery.IsVisibleTo(friendlyOwner, supply))
                {
                    continue;
                }

                float sqr = (supply.transform.position - civilCentralPosition).sqrMagnitude;
                if (sqr < minSqr)
                {
                    continue;
                }

                output.Add(supply.transform.position);
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
