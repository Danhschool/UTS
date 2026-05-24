using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Nhận diện mỏ food còn khai thác và trạng thái thiếu food trên kho.
    /// </summary>
    public static class AIEconomyFoodGatherUtility
    {
        /// <summary>
        /// Mục tiêu: Còn mỏ food visible và còn lượng để gather không.
        /// Cách hoạt động: Quét snapshot.GatherableSupplies; so SupplySO với config.
        /// </summary>
        public static bool HasVisibleFoodGatherNode(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config)
        {
            if (snapshot?.GatherableSupplies == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply node = snapshot.GatherableSupplies[i];
                if (node == null
                    || node.Amount <= 0
                    || node.Supply == null
                    || !FactionFogQuery.IsVisibleTo(snapshot.Owner, node))
                {
                    continue;
                }

                if (IsFoodSupply(config, node.Supply)
                    && !AIEconomyWildCorpseFoodUtility.IsWildAnimalCorpseGatherNode(node)
                    && AIEconomyGatherTerritoryGuard.IsGatherSupplyAllowed(snapshot, node))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Có nên ưu tiên bổ sung food (Corral / slot gather food) không.
        /// Cách hoạt động: Food dưới ngưỡng hoặc là loại ít nhất trong kho.
        /// </summary>
        public static bool IsFoodEconomicallyScarce(
            AIWorldStateSnapshot snapshot,
            AIEconomySettings settings)
        {
            if (snapshot == null || settings == null)
            {
                return false;
            }

            int food = Mathf.Max(0, snapshot.Food);
            if (food < settings.FoodLowAmountForExtraCorral)
            {
                return true;
            }

            int stone = Mathf.Max(0, snapshot.Stone);
            int wood = Mathf.Max(0, snapshot.Wood);
            return food <= stone && food <= wood;
        }

        public static bool IsFoodSupply(AIEconomyRuntimeConfig config, SupplySO supply)
        {
            if (supply == null)
            {
                return false;
            }

            if (config.FoodSupply != null && supply.Equals(config.FoodSupply))
            {
                return true;
            }

            return AIEconomySupplyKindClassifier.ClassifyByName(supply.name)
                   == AIEconomySupplyKindClassifier.Kind.Food;
        }
    }
}
