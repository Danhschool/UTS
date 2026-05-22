using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Cân bằng 40% đá / 40% gỗ / 20% food; mất cân bằng 60% thiếu + 20% mỗi loại kia.
    /// Hết mỏ food visible → chỉ đá/gỗ theo tỷ lệ 7:3 (phần lớn tối thiểu 7 worker khi đủ quy mô).
    /// </summary>
    public static class AIWorkerGatherSlotPlanner
    {
        private const float BalancedStoneShare = 0.4f;
        private const float BalancedWoodShare = 0.4f;
        private const float ImbalanceMinorShare = 0.2f;
        private const float StoneWoodOnlyMajorShare = 0.7f;
        private const int StoneWoodOnlyMinMajorSlots = 7;

        private sealed class BalanceCache
        {
            public int LastCheckPlannerTick;
            public bool IsBalanced;
            public SupplyKind ScarceKind;
        }

        private static readonly System.Collections.Generic.Dictionary<int, BalanceCache> BalanceByOwner = new(4);

        /// <summary>
        /// Mục tiêu: Số slot gather mỗi loại từ tổng worker và trạng thái cân bằng kho (cache 20 tick).
        /// </summary>
        public static void ComputeGatherSlotTargets(
            Owner owner,
            int plannerTickIndex,
            AIWorldStateSnapshot snapshot,
            AIEconomySettings settings,
            int gatherWorkerCount,
            bool hasVisibleFoodGatherNodes,
            out int stoneSlots,
            out int woodSlots,
            out int foodSlots)
        {
            stoneSlots = woodSlots = foodSlots = 0;
            if (gatherWorkerCount <= 0 || snapshot == null)
            {
                return;
            }

            if (!hasVisibleFoodGatherNodes
                && settings != null
                && settings.EnableStoneWoodOnlyWhenNoFoodMines)
            {
                ApplyStoneWoodOnlySplit(
                    gatherWorkerCount,
                    settings.StoneWoodMajorShare,
                    settings.StoneWoodMinMajorSlots,
                    out stoneSlots,
                    out woodSlots);
                return;
            }

            int interval = settings != null
                ? Mathf.Max(1, settings.GatherBalanceCheckIntervalTicks)
                : 20;
            float maxRatio = settings != null
                ? Mathf.Max(1.1f, settings.GatherImbalanceMaxRatio)
                : 3f;

            BalanceCache cache = GetOrCreate(owner);
            if (ShouldRunBalanceCheck(plannerTickIndex, cache.LastCheckPlannerTick, interval))
            {
                RefreshBalanceCache(cache, snapshot, maxRatio);
                cache.LastCheckPlannerTick = plannerTickIndex;
            }

            if (cache.IsBalanced)
            {
                stoneSlots = Mathf.FloorToInt(gatherWorkerCount * BalancedStoneShare);
                woodSlots = Mathf.FloorToInt(gatherWorkerCount * BalancedWoodShare);
                foodSlots = gatherWorkerCount - stoneSlots - woodSlots;
                return;
            }

            int minorSlots = Mathf.FloorToInt(gatherWorkerCount * ImbalanceMinorShare);
            int majorSlots = gatherWorkerCount - minorSlots * 2;
            stoneSlots = minorSlots;
            woodSlots = minorSlots;
            foodSlots = minorSlots;

            switch (cache.ScarceKind)
            {
                case SupplyKind.Stone:
                    stoneSlots = majorSlots;
                    break;
                case SupplyKind.Wood:
                    woodSlots = majorSlots;
                    break;
                default:
                    foodSlots = majorSlots;
                    break;
            }
        }

        private static bool ShouldRunBalanceCheck(int plannerTickIndex, int lastCheckTick, int interval) =>
            lastCheckTick < 0 || plannerTickIndex - lastCheckTick >= interval;

        private static void RefreshBalanceCache(BalanceCache cache, AIWorldStateSnapshot snapshot, float maxRatio)
        {
            int stone = Mathf.Max(0, snapshot.Stone);
            int wood = Mathf.Max(0, snapshot.Wood);
            int food = Mathf.Max(0, snapshot.Food);

            cache.IsBalanced = IsStockBalanced(stone, wood, food, maxRatio);
            cache.ScarceKind = ResolveScarcestKind(stone, wood, food);
        }

        /// <summary>
        /// Mục tiêu: Cân bằng khi max/min ≤ 3 (không chênh quá 3 lần).
        /// </summary>
        private static bool IsStockBalanced(int stone, int wood, int food, float maxRatio)
        {
            int min = Mathf.Min(stone, Mathf.Min(wood, food));
            int max = Mathf.Max(stone, Mathf.Max(wood, food));
            if (max <= 0)
            {
                return true;
            }

            if (min <= 0)
            {
                return false;
            }

            return (float)max / min <= maxRatio;
        }

        private static SupplyKind ResolveScarcestKind(int stone, int wood, int food)
        {
            if (stone <= wood && stone <= food)
            {
                return SupplyKind.Stone;
            }

            if (wood <= food)
            {
                return SupplyKind.Wood;
            }

            return SupplyKind.Food;
        }

        private static BalanceCache GetOrCreate(Owner owner)
        {
            int key = (int)owner;
            if (!BalanceByOwner.TryGetValue(key, out BalanceCache cache))
            {
                cache = new BalanceCache { LastCheckPlannerTick = -1, IsBalanced = true, ScarceKind = SupplyKind.Food };
                BalanceByOwner[key] = cache;
            }

            return cache;
        }

        /// <summary>
        /// Mục tiêu: Chia worker chỉ giữa đá và gỗ (70/30 mặc định; majority ≥ min khi tổng ≥ min).
        /// Cách hoạt động: stoneSlots = ceil/share có sàn min; woodSlots = phần còn lại.
        /// </summary>
        private static void ApplyStoneWoodOnlySplit(
            int gatherWorkerCount,
            float majorShare,
            int minMajorSlots,
            out int stoneSlots,
            out int woodSlots)
        {
            majorShare = Mathf.Clamp(majorShare, 0.51f, 0.95f);
            minMajorSlots = Mathf.Max(1, minMajorSlots);

            int major = Mathf.CeilToInt(gatherWorkerCount * majorShare);
            if (gatherWorkerCount >= minMajorSlots)
            {
                major = Mathf.Max(minMajorSlots, major);
            }

            major = Mathf.Clamp(major, 1, gatherWorkerCount);
            stoneSlots = major;
            woodSlots = gatherWorkerCount - stoneSlots;
        }

        internal enum SupplyKind
        {
            Stone,
            Wood,
            Food
        }
    }
}
