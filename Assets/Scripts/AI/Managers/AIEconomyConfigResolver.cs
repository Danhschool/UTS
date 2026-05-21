using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Suy ra thông số economy từ map + registry — không cần kéo asset/distance tay trên Inspector.
    /// </summary>
    public static class AIEconomyConfigResolver
    {
        private static readonly List<float> DistanceScratch = new(64);

        /// <summary>
        /// Mục tiêu: Một bộ thông số hoàn chỉnh cho tick hiện tại.
        /// Cách hoạt động: Quét mỏ/CC/worker commands; tính percentile khoảng cách; đọc footprint từ BuildingRestrictionSO.
        /// </summary>
        public static AIEconomyRuntimeConfig Resolve(AIWorldStateSnapshot snapshot, AIEconomySettings manualOverrides)
        {
            manualOverrides ??= AIEconomySettings.Default;

            ResolveSupplyTypes(snapshot, manualOverrides, out SupplySO stone, out SupplySO wood, out SupplySO food);
            BuildBuildingCommand storeCommand = ResolveStoreBuildCommand(snapshot, manualOverrides);

            Vector3 anchor = snapshot.CivilCentral != null
                ? snapshot.CivilCentral.transform.position
                : Vector3.zero;

            ComputeSupplyDistances(snapshot, anchor, DistanceScratch);

            float buildingFootprint = GetPlacementFootprint(storeCommand);
            float depositRadius = ComputeDepositSearchRadius(DistanceScratch);
            float remoteClusterMin = ComputeRemoteClusterMinDistance(DistanceScratch);
            float storeCoverage = ComputeStoreCoverageRadius(DistanceScratch, remoteClusterMin, buildingFootprint);
            float storeOffset = ComputeStoreOffset(storeCoverage, buildingFootprint);
            float deficitPenalty = ComputeDeficitPenalty(DistanceScratch);
            GetPlacementSearch(buildingFootprint, out int rings, out float step);

            return new AIEconomyRuntimeConfig(
                stone,
                wood,
                food,
                storeCommand,
                remoteClusterMin,
                storeCoverage,
                storeOffset,
                depositRadius,
                0.15f,
                deficitPenalty,
                rings,
                step);
        }

        private static void ResolveSupplyTypes(
            AIWorldStateSnapshot snapshot,
            AIEconomySettings overrides,
            out SupplySO stone,
            out SupplySO wood,
            out SupplySO food)
        {
            stone = overrides?.StoneSupply;
            wood = overrides?.WoodSupply;
            food = overrides?.FoodSupply;

            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply node = snapshot.GatherableSupplies[i];
                if (node?.Supply == null)
                {
                    continue;
                }

                SupplyKind kind = ClassifyByName(node.Supply);
                switch (kind)
                {
                    case SupplyKind.Stone when stone == null:
                        stone = node.Supply;
                        break;
                    case SupplyKind.Wood when wood == null:
                        wood = node.Supply;
                        break;
                    case SupplyKind.Food when food == null:
                        food = node.Supply;
                        break;
                }

                if (stone != null && wood != null && food != null)
                {
                    break;
                }
            }
        }

        private static BuildBuildingCommand ResolveStoreBuildCommand(
            AIWorldStateSnapshot snapshot,
            AIEconomySettings overrides)
        {
            if (overrides?.StoreBuildCommand != null)
            {
                return overrides.StoreBuildCommand;
            }

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker == null)
                {
                    continue;
                }

                List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
                for (int c = 0; c < commands.Count; c++)
                {
                    if (commands[c] is BuildBuildingCommand build
                        && build.Building != null
                        && build.Building.Name == SupplyDepositLocator.StoreHouseDisplayName)
                    {
                        return build;
                    }
                }
            }

            return null;
        }

        private static void ComputeSupplyDistances(
            AIWorldStateSnapshot snapshot,
            Vector3 anchor,
            List<float> distances)
        {
            distances.Clear();
            if (snapshot.CivilCentral == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply supply = snapshot.GatherableSupplies[i];
                if (supply == null || supply.Amount <= 0)
                {
                    continue;
                }

                float dist = Vector3.Distance(anchor, supply.transform.position);
                distances.Add(dist);
            }
        }

        /// <summary>
        /// Mục tiêu: Ngưỡng "mỏ xa" theo bản đồ thực tế (không hardcode 45m).
        /// Cách hoạt động: Percentile 60% khoảng cách CC→mỏ, clamp; tối thiểu 1.35× bán kính gần nhất.
        /// </summary>
        private static float ComputeRemoteClusterMinDistance(List<float> distances)
        {
            if (distances.Count < 2)
            {
                return float.MaxValue;
            }

            distances.Sort();
            float nearest = distances[0];
            float p60 = PercentileSorted(distances, 0.6f);
            float avg = Average(distances);
            float max = distances[distances.Count - 1];
            float blended = Mathf.Max(p60, avg + (max - avg) * 0.35f);
            return Mathf.Clamp(Mathf.Max(blended, nearest * 1.35f), 18f, 120f);
        }

        private static float ComputeStoreCoverageRadius(
            List<float> distances,
            float remoteClusterMin,
            float buildingFootprint)
        {
            float spread = 22f;
            if (distances.Count > 0)
            {
                float sum = 0f;
                for (int i = 0; i < distances.Count; i++)
                {
                    sum += distances[i];
                }

                float avg = sum / distances.Count;
                spread = Mathf.Max(18f, (avg - remoteClusterMin) * 0.45f + buildingFootprint);
            }

            return Mathf.Clamp(Mathf.Max(buildingFootprint * 1.75f, spread), 16f, 55f);
        }

        private static float ComputeStoreOffset(float storeCoverage, float buildingFootprint) =>
            Mathf.Clamp(storeCoverage * 0.4f, Mathf.Max(6f, buildingFootprint * 0.5f), 24f);

        private static float ComputeDepositSearchRadius(List<float> distances)
        {
            if (distances.Count == 0)
            {
                return 100f;
            }

            float max = distances[0];
            for (int i = 1; i < distances.Count; i++)
            {
                if (distances[i] > max)
                {
                    max = distances[i];
                }
            }

            return Mathf.Clamp(max * 1.35f + 30f, 70f, 280f);
        }

        private static float ComputeDeficitPenalty(List<float> distances)
        {
            if (distances.Count == 0)
            {
                return 60f;
            }

            float avg = Average(distances);
            return Mathf.Clamp(avg * 0.85f, 45f, 140f);
        }

        private static void GetPlacementSearch(
            float buildingFootprint,
            out int rings,
            out float step)
        {
            step = Mathf.Clamp(buildingFootprint * 0.45f, 3f, 8f);
            rings = Mathf.Clamp(Mathf.CeilToInt(buildingFootprint / step) + 2, 4, 8);
        }

        private static float GetPlacementFootprint(BuildBuildingCommand storeCommand)
        {
            if (storeCommand?.Restrictions == null || storeCommand.Restrictions.Length == 0)
            {
                return 10f;
            }

            float footprint = 4f;
            BuildingRestrictionSO[] restrictions = storeCommand.Restrictions;
            for (int i = 0; i < restrictions.Length; i++)
            {
                BuildingRestrictionSO restriction = restrictions[i];
                if (restriction == null)
                {
                    continue;
                }

                footprint = Mathf.Max(
                    footprint,
                    restriction.Radius,
                    restriction.Extents.x,
                    restriction.Extents.z);
            }

            return footprint;
        }

        private static float PercentileSorted(List<float> sortedAsc, float percentile)
        {
            if (sortedAsc.Count == 0)
            {
                return 0f;
            }

            if (sortedAsc.Count == 1)
            {
                return sortedAsc[0];
            }

            float index = percentile * (sortedAsc.Count - 1);
            int lower = Mathf.FloorToInt(index);
            int upper = Mathf.CeilToInt(index);
            if (lower == upper)
            {
                return sortedAsc[lower];
            }

            float t = index - lower;
            return Mathf.Lerp(sortedAsc[lower], sortedAsc[upper], t);
        }

        private static float Average(List<float> values)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum / values.Count;
        }

        private static SupplyKind ClassifyByName(SupplySO supply)
        {
            string name = supply.name;
            if (name.Contains("Stone", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Stone;
            }

            if (name.Contains("Wood", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Wood;
            }

            if (name.Contains("Food", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Food;
            }

            return SupplyKind.Unknown;
        }

        private enum SupplyKind
        {
            Unknown,
            Stone,
            Wood,
            Food
        }
    }

    /// <summary>Thông số economy đã resolve cho một tick — read-only.</summary>
    public readonly struct AIEconomyRuntimeConfig
    {
        public SupplySO StoneSupply { get; }
        public SupplySO WoodSupply { get; }
        public SupplySO FoodSupply { get; }
        public BuildBuildingCommand StoreBuildCommand { get; }
        public float RemoteClusterMinDistance { get; }
        public float StoreCoverageRadius { get; }
        public float StoreOffsetFromCluster { get; }
        public float DepositSearchRadius { get; }
        public float AnchorDistanceWeight { get; }
        public float NonDeficitSupplyDistancePenalty { get; }
        public int PlacementSearchRings { get; }
        public float PlacementSearchStep { get; }

        public AIEconomyRuntimeConfig(
            SupplySO stoneSupply,
            SupplySO woodSupply,
            SupplySO foodSupply,
            BuildBuildingCommand storeBuildCommand,
            float remoteClusterMinDistance,
            float storeCoverageRadius,
            float storeOffsetFromCluster,
            float depositSearchRadius,
            float anchorDistanceWeight,
            float nonDeficitSupplyDistancePenalty,
            int placementSearchRings,
            float placementSearchStep)
        {
            StoneSupply = stoneSupply;
            WoodSupply = woodSupply;
            FoodSupply = foodSupply;
            StoreBuildCommand = storeBuildCommand;
            RemoteClusterMinDistance = remoteClusterMinDistance;
            StoreCoverageRadius = storeCoverageRadius;
            StoreOffsetFromCluster = storeOffsetFromCluster;
            DepositSearchRadius = depositSearchRadius;
            AnchorDistanceWeight = anchorDistanceWeight;
            NonDeficitSupplyDistancePenalty = nonDeficitSupplyDistancePenalty;
            PlacementSearchRings = placementSearchRings;
            PlacementSearchStep = placementSearchStep;
        }
    }
}
