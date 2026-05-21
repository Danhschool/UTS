namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Builds display text for a single log line (TMP rich text).
    /// </summary>
    public static class GameEventLogLineFormatter
    {
        public static string Format(GameEventLogLine line, bool showTimestamps)
        {
            if (showTimestamps)
            {
                int minutes = UnityEngine.Mathf.FloorToInt(line.TimeSeconds / 60f);
                int seconds = UnityEngine.Mathf.FloorToInt(line.TimeSeconds % 60f);
                return $"[{minutes:00}:{seconds:00}] {GetCategoryPrefix(line.Category)}{line.Message}";
            }

            return $"{GetCategoryPrefix(line.Category)}{line.Message}";
        }

        private static string GetCategoryPrefix(GameEventLogCategory category) => category switch
        {
            GameEventLogCategory.AI => "<color=#9AE6B0>[AI]</color> ",
            GameEventLogCategory.Resource => "<color=#E6A817>[Tài nguyên]</color> ",
            GameEventLogCategory.Build => "<color=#7EC8FF>[Xây dựng]</color> ",
            GameEventLogCategory.Combat => "<color=#FF6B6B>[Chiến đấu]</color> ",
            GameEventLogCategory.Warning => "<color=#FFB347>[Cảnh báo]</color> ",
            GameEventLogCategory.GameOver => "<color=#FFD700>[Kết quả]</color> ",
            _ => ""
        };
    }
}
