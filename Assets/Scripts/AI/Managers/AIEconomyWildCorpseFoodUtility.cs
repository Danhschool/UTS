using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phân biệt mỏ food tĩnh (berry, farm) với xác thú sau săn — xác không được coi là "mỏ food" cho slot 7:3 đá-gỗ.
    /// </summary>
    public static class AIEconomyWildCorpseFoodUtility
    {
        /// <summary>
        /// Mục tiêu: Planner không kích hoạt chế độ "còn mỏ food" chỉ vì xác Deer/Goat trên map.
        /// Cách hoạt động: Heuristic tên prefab spawn (…Die…) — không GetComponent mỗi lần classify.
        /// </summary>
        public static bool IsWildAnimalCorpseGatherNode(GatherableSupply supply) =>
            supply != null
            && supply.name.Contains("Die", System.StringComparison.OrdinalIgnoreCase);
    }
}
