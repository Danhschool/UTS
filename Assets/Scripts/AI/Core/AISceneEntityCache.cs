using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Cache entity toàn scene tối đa một lần mỗi frame — tránh FindObjectsByType lặp trong military/economy.
    /// </summary>
    public static class AISceneEntityCache
    {
        const int DefaultMinFramesBetweenRefresh = 18;

        static int cachedFrame = -1;
        static int minFramesBetweenRefresh = DefaultMinFramesBetweenRefresh;
        static BaseBuilding[] buildings = System.Array.Empty<BaseBuilding>();
        static AbstractUnit[] units = System.Array.Empty<AbstractUnit>();
        static WildAnimal[] wildAnimals = System.Array.Empty<WildAnimal>();

        /// <summary>
        /// Mục tiêu: Giảm FindObjectsByType khi nhiều subsystem gọi trong vài frame liên tiếp.
        /// Cách hoạt động: Clamp interval ≥ 1; áp dụng ngay lần EnsureFresh kế tiếp.
        /// </summary>
        public static void SetMinFramesBetweenRefresh(int frames)
        {
            minFramesBetweenRefresh = Mathf.Max(1, frames);
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo cache entity trước planner (military / wild food / sight fallback).
        /// Cách hoạt động: Refresh tối đa một lần mỗi minFramesBetweenRefresh frame; force bỏ qua throttle.
        /// </summary>
        public static void EnsureFresh(bool force = false)
        {
            int frame = Time.frameCount;
            if (!force
                && cachedFrame >= 0
                && frame - cachedFrame < minFramesBetweenRefresh)
            {
                return;
            }

            if (!force && cachedFrame == frame)
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
