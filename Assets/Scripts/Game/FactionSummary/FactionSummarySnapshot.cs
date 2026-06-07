using System.Collections.Generic;

namespace GameDevTV.RTS.Game.FactionSummary
{
    /// <summary>
    /// Một nhóm thống kê: tiêu đề + các chỉ số nằm trên cùng một hàng ngang.
    /// </summary>
    public sealed class FactionSummarySection
    {
        readonly List<FactionSummaryCountEntry> entries = new(8);

        public string Title { get; private set; }
        public IReadOnlyList<FactionSummaryCountEntry> Entries => entries;

        public void SetTitle(string title)
        {
            Title = title;
        }

        public void AddEntry(string label, int current, int total)
        {
            entries.Add(new FactionSummaryCountEntry(label, current, total));
        }
    }

    /// <summary>
    /// Snapshot read-only cho UI tóm tắt phe tại một thời điểm.
    /// </summary>
    public sealed class FactionSummarySnapshot
    {
        public static readonly FactionSummarySnapshot Empty = new();

        readonly List<FactionSummarySection> sections = new(4);
        FactionSummarySection activeSection;

        public IReadOnlyList<FactionSummarySection> Sections => sections;

        public void Clear()
        {
            sections.Clear();
            activeSection = null;
        }

        public FactionSummarySection BeginSection(string title)
        {
            activeSection = new FactionSummarySection();
            activeSection.SetTitle(title);
            sections.Add(activeSection);
            return activeSection;
        }

        public void AddEntry(string label, int current, int total)
        {
            activeSection?.AddEntry(label, current, total);
        }

        /// <summary>
        /// Mục tiêu: Sao chép snapshot trước khi hủy FactionSummaryTracker / load scene End.
        /// Cách hoạt động: Tạo snapshot mới và copy từng section + entry.
        /// </summary>
        public static FactionSummarySnapshot CloneFrom(FactionSummarySnapshot source)
        {
            var clone = new FactionSummarySnapshot();
            if (source == null)
            {
                return clone;
            }

            IReadOnlyList<FactionSummarySection> sourceSections = source.Sections;
            for (int i = 0; i < sourceSections.Count; i++)
            {
                FactionSummarySection section = sourceSections[i];
                if (section == null)
                {
                    continue;
                }

                FactionSummarySection cloneSection = clone.BeginSection(section.Title);
                IReadOnlyList<FactionSummaryCountEntry> entries = section.Entries;
                for (int j = 0; j < entries.Count; j++)
                {
                    FactionSummaryCountEntry entry = entries[j];
                    cloneSection.AddEntry(entry.Label, entry.Current, entry.Total);
                }
            }

            return clone;
        }
    }
}
