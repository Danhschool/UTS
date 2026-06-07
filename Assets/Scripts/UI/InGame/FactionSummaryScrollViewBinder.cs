using System.Collections.Generic;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Đổ snapshot thống kê phe vào ScrollView Content (section title + hàng ngang).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryScrollViewBinder : MonoBehaviour
    {
        enum RuntimeBlockKind
        {
            SectionTitle = 0,
            HorizontalStats = 1
        }

        struct RuntimeBlock
        {
            public GameObject Root;
            public RuntimeBlockKind Kind;
        }

        [SerializeField] Transform contentRoot;
        [SerializeField] GameObject sectionTitlePrefab;
        [SerializeField] GameObject horizontalRowPrefab;
        [SerializeField] GameObject statChipPrefab;
        [SerializeField] GameObject summaryRowPrefab;
        [SerializeField] bool clearExistingRowsOnRefresh = true;
        [SerializeField] Owner ownerOverride = Owner.Invalid;

        readonly List<RuntimeBlock> runtimeBlocks = new(16);
        readonly FactionSummarySnapshot snapshotCache = new();

        /// <summary>
        /// Mục tiêu: Cập nhật panel tóm tắt theo phe local.
        /// Cách hoạt động: Mỗi section = 1 tiêu đề + 1 hàng ngang các stat chip.
        /// </summary>
        public void Refresh()
        {
            ResolveReferences();

            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            Owner owner = ownerOverride != Owner.Invalid
                ? ownerOverride
                : LocalHumanOwnerAccess.GetLocalOwnerOrDefault();

            tracker.BuildSnapshot(owner, snapshotCache);
            RebuildSections(snapshotCache.Sections);
        }

        /// <summary>
        /// Mục tiêu: Panel kết thúc trận — thống kê 2 phe (Bạn + Đối phương).
        /// Cách hoạt động: Mỗi phe = header + 3 section ngang giống Tab summary.
        /// </summary>
        public void RefreshDual(Owner localOwner, Owner opponentOwner)
        {
            ResolveReferences();

            if (contentRoot == null)
            {
                Debug.LogWarning($"{nameof(FactionSummaryScrollViewBinder)}: thiếu Content.", this);
                return;
            }

            if (clearExistingRowsOnRefresh)
            {
                ClearRuntimeBlocks();
            }

            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            AppendOwnerSummaryBlock(
                MatchOutcomeResolver.GetFactionLabel(localOwner, localOwner),
                localOwner,
                tracker);
            AppendOwnerSummaryBlock(
                MatchOutcomeResolver.GetFactionLabel(opponentOwner, localOwner),
                opponentOwner,
                tracker);
        }

        /// <summary>
        /// Mục tiêu: Scene End — hiển thị snapshot đã chụp trước khi load scene (không cần tracker).
        /// </summary>
        public void RefreshDualFromSession(
            string localFactionLabel,
            FactionSummarySnapshot localSummary,
            string opponentFactionLabel,
            FactionSummarySnapshot opponentSummary)
        {
            ResolveReferences();

            if (contentRoot == null)
            {
                Debug.LogWarning($"{nameof(FactionSummaryScrollViewBinder)}: thiếu Content.", this);
                return;
            }

            if (clearExistingRowsOnRefresh)
            {
                ClearRuntimeBlocks();
            }

            AppendOwnerSummaryFromSnapshot(localFactionLabel, localSummary);
            AppendOwnerSummaryFromSnapshot(opponentFactionLabel, opponentSummary);
        }

        void AppendOwnerSummaryFromSnapshot(string factionHeader, FactionSummarySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            AddSectionTitleBlock(factionHeader);

            IReadOnlyList<FactionSummarySection> sections = snapshot.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                FactionSummarySection section = sections[i];
                if (section == null)
                {
                    continue;
                }

                AddSectionTitleBlock(section.Title);
                AddHorizontalStatsBlock(section.Entries);
            }
        }

        void AppendOwnerSummaryBlock(string factionHeader, Owner owner, FactionSummaryTracker tracker)
        {
            AddSectionTitleBlock(factionHeader);

            tracker.BuildSnapshot(owner, snapshotCache);
            IReadOnlyList<FactionSummarySection> sections = snapshotCache.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                FactionSummarySection section = sections[i];
                if (section == null)
                {
                    continue;
                }

                AddSectionTitleBlock(section.Title);
                AddHorizontalStatsBlock(section.Entries);
            }
        }

        void AddSectionTitleBlock(string title)
        {
            GameObject instance = InstantiateSectionBlock(RuntimeBlockKind.SectionTitle);
            BindSectionTitle(instance, title);
        }

        void AddHorizontalStatsBlock(IReadOnlyList<FactionSummaryCountEntry> entries)
        {
            GameObject instance = InstantiateSectionBlock(RuntimeBlockKind.HorizontalStats);
            BindHorizontalStats(instance, entries);
        }

        GameObject InstantiateSectionBlock(RuntimeBlockKind kind)
        {
            GameObject prefab = kind == RuntimeBlockKind.SectionTitle
                ? sectionTitlePrefab
                : horizontalRowPrefab;

            GameObject instance = Instantiate(prefab, contentRoot);
            instance.name = kind == RuntimeBlockKind.SectionTitle
                ? $"OutcomeSectionTitle_{runtimeBlocks.Count}"
                : $"OutcomeHorizontalRow_{runtimeBlocks.Count}";

            if (kind == RuntimeBlockKind.HorizontalStats)
            {
                FactionSummaryHorizontalRowView row = instance.GetComponent<FactionSummaryHorizontalRowView>();
                if (row == null)
                {
                    row = instance.AddComponent<FactionSummaryHorizontalRowView>();
                }

                row.SetChipPrefab(statChipPrefab);
            }

            runtimeBlocks.Add(new RuntimeBlock
            {
                Root = instance,
                Kind = kind
            });

            return instance;
        }

        void RebuildSections(IReadOnlyList<FactionSummarySection> sections)
        {
            if (contentRoot == null)
            {
                Debug.LogWarning($"{nameof(FactionSummaryScrollViewBinder)}: thiếu Content.", this);
                return;
            }

            if (clearExistingRowsOnRefresh)
            {
                ClearRuntimeBlocks();
            }

            int requiredBlocks = sections.Count * 2;
            EnsureBlockCount(requiredBlocks);

            int blockIndex = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                FactionSummarySection section = sections[i];
                if (section == null)
                {
                    continue;
                }

                BindSectionTitle(runtimeBlocks[blockIndex++].Root, section.Title);
                BindHorizontalStats(runtimeBlocks[blockIndex++].Root, section.Entries);
            }

            for (int i = blockIndex; i < runtimeBlocks.Count; i++)
            {
                if (runtimeBlocks[i].Root != null)
                {
                    runtimeBlocks[i].Root.SetActive(false);
                }
            }
        }

        void BindSectionTitle(GameObject root, string title)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(true);
            FactionSummarySectionTitleView view = root.GetComponent<FactionSummarySectionTitleView>();
            if (view == null)
            {
                view = root.AddComponent<FactionSummarySectionTitleView>();
            }

            view.SetTitle(title);
        }

        void BindHorizontalStats(GameObject root, IReadOnlyList<FactionSummaryCountEntry> entries)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(true);
            FactionSummaryHorizontalRowView row = root.GetComponent<FactionSummaryHorizontalRowView>();
            if (row == null)
            {
                row = root.AddComponent<FactionSummaryHorizontalRowView>();
            }

            row.SetChipPrefab(statChipPrefab);
            row.SetEntries(entries);
        }

        void EnsureBlockCount(int count)
        {
            while (runtimeBlocks.Count < count)
            {
                RuntimeBlockKind kind = runtimeBlocks.Count % 2 == 0
                    ? RuntimeBlockKind.SectionTitle
                    : RuntimeBlockKind.HorizontalStats;

                GameObject prefab = kind == RuntimeBlockKind.SectionTitle
                    ? sectionTitlePrefab
                    : horizontalRowPrefab;

                GameObject instance = Instantiate(prefab, contentRoot);
                instance.name = kind == RuntimeBlockKind.SectionTitle
                    ? $"SummarySectionTitle_{runtimeBlocks.Count / 2}"
                    : $"SummaryHorizontalRow_{runtimeBlocks.Count / 2}";

                if (kind == RuntimeBlockKind.HorizontalStats)
                {
                    FactionSummaryHorizontalRowView row = instance.GetComponent<FactionSummaryHorizontalRowView>();
                    if (row == null)
                    {
                        row = instance.AddComponent<FactionSummaryHorizontalRowView>();
                    }
                }

                runtimeBlocks.Add(new RuntimeBlock
                {
                    Root = instance,
                    Kind = kind
                });
            }
        }

        void ClearRuntimeBlocks()
        {
            for (int i = runtimeBlocks.Count - 1; i >= 0; i--)
            {
                if (runtimeBlocks[i].Root != null)
                {
                    Destroy(runtimeBlocks[i].Root);
                }
            }

            runtimeBlocks.Clear();
        }

        void ResolveReferences()
        {
            if (contentRoot == null)
            {
                ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null && scrollRect.content != null)
                {
                    contentRoot = scrollRect.content;
                }
            }

            if (statChipPrefab == null && summaryRowPrefab != null)
            {
                statChipPrefab = summaryRowPrefab;
            }

            if (statChipPrefab == null && contentRoot != null)
            {
                Transform template = contentRoot.Find("SummaryRow Template");
                if (template != null)
                {
                    statChipPrefab = template.gameObject;
                }
            }

            EnsureHorizontalRowPrefab();
            EnsureSectionTitlePrefab();
        }

        void EnsureHorizontalRowPrefab()
        {
            if (horizontalRowPrefab != null)
            {
                return;
            }

            horizontalRowPrefab = new GameObject(
                "SummaryHorizontalRow Template",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(FactionSummaryHorizontalRowView));
            horizontalRowPrefab.transform.SetParent(contentRoot, false);
            horizontalRowPrefab.SetActive(false);
        }

        void EnsureSectionTitlePrefab()
        {
            if (sectionTitlePrefab != null)
            {
                return;
            }

            sectionTitlePrefab = new GameObject(
                "SummarySectionTitle Template",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(FactionSummarySectionTitleView));

            GameObject textObject = new("Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            textObject.transform.SetParent(sectionTitlePrefab.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            sectionTitlePrefab.transform.SetParent(contentRoot, false);
            sectionTitlePrefab.SetActive(false);
        }
    }
}
