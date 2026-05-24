using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Cache entity toàn scene tối đa một lần mỗi frame — tránh FindObjectsByType lặp trong military/economy.
    /// </summary>
    public static class AISceneEntityCache
    {
        static int cachedFrame = -1;
        static BaseBuilding[] buildings = System.Array.Empty<BaseBuilding>();
        static AbstractUnit[] units = System.Array.Empty<AbstractUnit>();
        static WildAnimal[] wildAnimals = System.Array.Empty<WildAnimal>();

        /// <summary>
        /// Mục tiêu: Đảm bảo mảng cache đồng bộ với frame hiện tại trước planner.
        /// Cách hoạt động: So Time.frameCount; nếu đổi frame thì FindObjectsByType một lần.
        /// </summary>
        public static void EnsureFresh()
        {
            int frame = Time.frameCount;
            if (cachedFrame == frame)
            {
                return;
            }

            cachedFrame = frame;
            buildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            units = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            wildAnimals = Object.FindObjectsByType<WildAnimal>(FindObjectsSortMode.None);
        }

        public static BaseBuilding[] Buildings => buildings;
        public static AbstractUnit[] Units => units;
        public static WildAnimal[] WildAnimals => wildAnimals;
    }
}
