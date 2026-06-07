namespace GameDevTV.RTS.Game.FactionSummary
{
    /// <summary>
    /// Một dòng thống kê: nhãn + số hiện tại / tổng (kể cả đã dùng hoặc mất).
    /// </summary>
    public readonly struct FactionSummaryCountEntry
    {
        public FactionSummaryCountEntry(string label, int current, int total, bool isSectionHeader = false)
        {
            Label = label;
            Current = current;
            Total = total;
            IsSectionHeader = isSectionHeader;
        }

        public string Label { get; }
        public int Current { get; }
        public int Total { get; }
        public bool IsSectionHeader { get; }

        public string FormatValue()
        {
            if (IsSectionHeader)
            {
                return string.Empty;
            }

            return $"{Current}/{Total}";
        }
    }
}
