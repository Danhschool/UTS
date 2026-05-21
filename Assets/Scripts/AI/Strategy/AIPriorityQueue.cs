using System.Collections.Generic;

namespace GameDevTV.RTS.AI
{
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
