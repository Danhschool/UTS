using System;
using System.Collections.Generic;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// In-memory event log buffer; UI and other systems subscribe to <see cref="LineAdded"/>.
    /// </summary>
    public static class GameEventLog
    {
        public static event Action<GameEventLogLine> LineAdded;

        private static readonly List<GameEventLogLine> lines = new();
        private static int maxLines = 50;

        public static IReadOnlyList<GameEventLogLine> Lines => lines;

        public static int MaxLines
        {
            get => maxLines;
            set => maxLines = Math.Max(1, value);
        }

        /// <summary>
        /// Mục tiêu: Thêm dòng thông báo vào khung chat sự kiện.
        /// Cách hoạt động: Lưu vào buffer (giới hạn maxLines), raise LineAdded cho UI cập nhật.
        /// </summary>
        public static void Post(string message, GameEventLogCategory category = GameEventLogCategory.Info)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            GameEventLogLine line = new(message.Trim(), category, UnityEngine.Time.time);
            lines.Add(line);

            while (lines.Count > maxLines)
            {
                lines.RemoveAt(0);
            }

            LineAdded?.Invoke(line);
        }

        public static void Clear()
        {
            lines.Clear();
        }
    }
}
