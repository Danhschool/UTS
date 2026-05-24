using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phân loại <see cref="SupplySO"/> thành đá / gỗ / food cho economy AI.
    /// </summary>
    public static class AIEconomySupplyKindClassifier
    {
        public enum Kind
        {
            Unknown = 0,
            Stone = 1,
            Wood = 2,
            Food = 3
        }

        /// <summary>
        /// Mục tiêu: Planner biết mỏ đá/gỗ/food (kể cả asset QPS_Rock, QPS_Pine…).
        /// Cách hoạt động: So reference SO đã resolve; fallback tên asset (Rock→Stone, Pine/Cypress→Wood).
        /// </summary>
        public static Kind Classify(
            SupplySO supply,
            SupplySO stoneReference = null,
            SupplySO woodReference = null,
            SupplySO foodReference = null)
        {
            if (supply == null)
            {
                return Kind.Unknown;
            }

            if (stoneReference != null && supply.Equals(stoneReference))
            {
                return Kind.Stone;
            }

            if (woodReference != null && supply.Equals(woodReference))
            {
                return Kind.Wood;
            }

            if (foodReference != null && supply.Equals(foodReference))
            {
                return Kind.Food;
            }

            return ClassifyByName(supply.name);
        }

        /// <summary>
        /// Mục tiêu: Quét map lần đầu gán StoneSupply/WoodSupply/FoodSupply khi Inspector để trống.
        /// Cách hoạt động: Cùng heuristic tên với <see cref="Classify"/>.
        /// </summary>
        public static Kind ClassifyByName(string supplyAssetName)
        {
            if (string.IsNullOrEmpty(supplyAssetName))
            {
                return Kind.Unknown;
            }

            if (MatchesStone(supplyAssetName))
            {
                return Kind.Stone;
            }

            if (MatchesWood(supplyAssetName))
            {
                return Kind.Wood;
            }

            if (MatchesFood(supplyAssetName))
            {
                return Kind.Food;
            }

            return Kind.Unknown;
        }

        static bool MatchesStone(string name) =>
            name.Contains("Stone", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Rock", System.StringComparison.OrdinalIgnoreCase);

        static bool MatchesWood(string name) =>
            name.Contains("Wood", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Pine", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cypress", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Conifer", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Tree", System.StringComparison.OrdinalIgnoreCase);

        static bool MatchesFood(string name) =>
            name.Contains("Food", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Berry", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bush", System.StringComparison.OrdinalIgnoreCase);
    }
}
