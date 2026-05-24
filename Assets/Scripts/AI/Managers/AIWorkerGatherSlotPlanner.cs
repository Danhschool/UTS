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

            float maxRatio = settings != null
                ? Mathf.Max(1.1f, settings.GatherImbalanceMaxRatio)
                : 3f;

            if (!hasVisibleFoodGatherNodes
                && settings != null
                && settings.EnableStoneWoodOnlyWhenNoFoodMines)
            {
                ApplyStoneWoodSplitFromStock(
                    gatherWorkerCount,
                    snapshot,
                    maxRatio,
                    settings.StoneWoodMajorShare,
                    out stoneSlots,
                    out woodSlots);
                TryReserveFoodSlotWhenStockLow(snapshot, settings, gatherWorkerCount, ref stoneSlots, ref woodSlots, ref foodSlots);
                return;
            }

            int interval = settings != null
                ? Mathf.Max(1, settings.GatherBalanceCheckIntervalTicks)
                : 20;

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
        /// Mục tiêu: Không còn mỏ food tĩnh — chia worker đá/gỗ theo kho (giống 40/40 hoặc 60% thiếu).
        /// Cách hoạt động: Cân bằng → ~50/50; lệch → majorShare (mặc định 0.7) cho loại ít trong kho; luôn ≥1 gỗ nếu ≥2 worker.
        /// </summary>
        static void ApplyStoneWoodSplitFromStock(
            int gatherWorkerCount,
            AIWorldStateSnapshot snapshot,
            float maxRatio,
            float majorShareWhenImbalanced,
            out int stoneSlots,
            out int woodSlots)
        {
            stoneSlots = woodSlots = 0;
            if (gatherWorkerCount <= 0 || snapshot == null)
            {
                return;
            }

            if (gatherWorkerCount == 1)
            {
                stoneSlots = 1;
                return;
            }

            int stone = Mathf.Max(0, snapshot.Stone);
            int wood = Mathf.Max(0, snapshot.Wood);
            majorShareWhenImbalanced = Mathf.Clamp(majorShareWhenImbalanced, 0.51f, 0.95f);

            if (IsStoneWoodStockBalanced(stone, wood, maxRatio))
            {
                woodSlots = Mathf.Max(1, gatherWorkerCount / 2);
                stoneSlots = gatherWorkerCount - woodSlots;
                return;
            }

            int majorSlots = Mathf.Max(1, Mathf.RoundToInt(gatherWorkerCount * majorShareWhenImbalanced));
            majorSlots = Mathf.Min(majorSlots, gatherWorkerCount - 1);

            if (stone <= wood)
            {
                stoneSlots = majorSlots;
                woodSlots = gatherWorkerCount - stoneSlots;
            }
            else
            {
                woodSlots = majorSlots;
                stoneSlots = gatherWorkerCount - stoneSlots;
            }
        }

        static bool IsStoneWoodStockBalanced(int stone, int wood, float maxRatio)
        {
            int max = Mathf.Max(stone, wood);
            int min = Mathf.Min(stone, wood);
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

        /// <summary>
        /// Mục tiêu: Chế độ chỉ đá/gỗ vẫn có 1 slot food khi kho thấp (xác thú / săn).
        /// Cách hoạt động: Lấy 1 từ stone hoặc wood nếu còn dư slot.
        /// </summary>
        static void TryReserveFoodSlotWhenStockLow(
            AIWorldStateSnapshot snapshot,
            AIEconomySettings settings,
            int gatherWorkerCount,
            ref int stoneSlots,
            ref int woodSlots,
            ref int foodSlots)
        {
            if (snapshot == null
                || settings == null
                || gatherWorkerCount < 3
                || foodSlots > 0
                || snapshot.Food >= settings.FoodHuntBelowAmount)
            {
                return;
            }

            if (stoneSlots > 1)
            {
                stoneSlots--;
                foodSlots = 1;
                return;
            }

            if (woodSlots > 1)
            {
                woodSlots--;
                foodSlots = 1;
            }
        }

        internal enum SupplyKind
        {
            Stone,
            Wood,
            Food
        }
    }
}
