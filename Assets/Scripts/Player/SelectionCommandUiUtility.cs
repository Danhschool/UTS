using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Lọc selection trước khi hiển thị action bar MP — worker lệnh build không lẫn CC/nhà khác.
    /// </summary>
    public static class SelectionCommandUiUtility
    {
        /// <summary>
        /// Mục tiêu: Khi có Worker trong selection, chỉ giữ unit di động (không CC / nhà).
        /// Cách hoạt động: CC spawn đầu trận vẫn chọn riêng được; lẫn worker thì gỡ mọi BaseBuilding.
        /// </summary>
        public static void PruneBuildingsWhenWorkersPresent(List<AbstractCommandable> buffer)
        {
            if (buffer == null || buffer.Count == 0)
            {
                return;
            }

            bool hasWorker = false;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] is Worker)
                {
                    hasWorker = true;
                    break;
                }
            }

            if (!hasWorker)
            {
                return;
            }

            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                if (buffer[i] is BaseBuilding)
                {
                    buffer.RemoveAt(i);
                }
            }
        }
    }
}
