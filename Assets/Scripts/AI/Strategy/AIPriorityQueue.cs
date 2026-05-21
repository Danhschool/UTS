using System.Collections.Generic;

namespace GameDevTV.RTS.AI
{
    /// <summary>Priority bands theo plan V2 (cao → thấp).</summary>
    public static class AIPriorityBands
    {
        public const int Critical = 900;
        public const int High = 700;
        public const int Medium = 500;
        public const int Low = 300;
        public const int Background = 100;
    }

    /// <summary>
    /// Hàng đợi intent theo priority — manager enqueue, controller pop mỗi tick.
    /// </summary>
    public sealed class AIPriorityQueue
    {
        private readonly List<AICommandIntent> intents = new(32);

        public int Count => intents.Count;

        public void Clear() => intents.Clear();

        /// <summary>
        /// Mục tiêu: Thêm intent từ planner (economy/base/military).
        /// Cách hoạt động: Append vào list; sort khi pop.
        /// </summary>
        public void Enqueue(AICommandIntent intent) => intents.Add(intent);

        /// <summary>
        /// Mục tiêu: Lấy intent priority cao nhất.
        /// Cách hoạt động: Sort descending một lần; pop phần tử đầu.
        /// </summary>
        public bool TryPop(out AICommandIntent intent)
        {
            if (intents.Count == 0)
            {
                intent = default;
                return false;
            }

            intents.Sort();
            intent = intents[0];
            intents.RemoveAt(0);
            return true;
        }
    }
}
