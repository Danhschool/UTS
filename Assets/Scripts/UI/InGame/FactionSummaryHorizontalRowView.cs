using System.Collections.Generic;
using GameDevTV.RTS.Game.FactionSummary;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Một hàng ngang chứa nhiều ô thống kê cùng loại (3 tài nguyên, nhiều unit, …).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryHorizontalRowView : MonoBehaviour
    {
        [SerializeField] Transform chipRoot;
        [SerializeField] GameObject statChipPrefab;
        [SerializeField] float rowHeight = 32f;
        [SerializeField] float chipSpacing = 20f;

        readonly List<GameObject> runtimeChips = new(12);

        void Awake()
        {
            ResolveReferences();
            EnsureRowLayout();
        }

        public void SetChipPrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                statChipPrefab = prefab;
            }
        }

        /// <summary>
        /// Mục tiêu: Hiển thị toàn bộ chỉ số của một section trên một hàng.
        /// Cách hoạt động: Instantiate/reuse stat chip prefab theo số entry.
        /// </summary>
        public void SetEntries(IReadOnlyList<FactionSummaryCountEntry> entries)
        {
            ResolveReferences();
            EnsureRowLayout();

            if (chipRoot == null || statChipPrefab == null || entries == null)
            {
                return;
            }

            EnsureChipCount(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                FactionSummaryCountEntry entry = entries[i];
                GameObject chipObject = runtimeChips[i];
                if (chipObject == null)
                {
                    continue;
                }

                FactionSummaryStatChipView chip = chipObject.GetComponent<FactionSummaryStatChipView>();
                if (chip == null)
                {
                    chip = chipObject.AddComponent<FactionSummaryStatChipView>();
                }

                chip.SetData(entry.Label, entry.Current, entry.Total);
            }
        }

        void EnsureChipCount(int count)
        {
            while (runtimeChips.Count < count)
            {
                GameObject instance = Instantiate(statChipPrefab, chipRoot);
                instance.name = $"StatChip_{runtimeChips.Count}";
                runtimeChips.Add(instance);
            }

            for (int i = 0; i < runtimeChips.Count; i++)
            {
                if (runtimeChips[i] != null)
                {
                    runtimeChips[i].SetActive(i < count);
                }
            }
        }

        void EnsureRowLayout()
        {
            LayoutElement rowLayout = GetComponent<LayoutElement>();
            if (rowLayout == null)
            {
                rowLayout = gameObject.AddComponent<LayoutElement>();
            }

            rowLayout.minHeight = rowHeight;
            rowLayout.preferredHeight = rowHeight;

            HorizontalLayoutGroup horizontal = chipRoot != null
                ? chipRoot.GetComponent<HorizontalLayoutGroup>()
                : null;
            if (horizontal == null && chipRoot != null)
            {
                horizontal = chipRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            if (horizontal != null)
            {
                horizontal.spacing = chipSpacing;
                horizontal.childAlignment = TextAnchor.MiddleLeft;
                horizontal.childControlWidth = true;
                horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = true;
                horizontal.childForceExpandHeight = false;
                horizontal.padding = new RectOffset(0, 0, 0, 4);
            }
        }

        void ResolveReferences()
        {
            if (chipRoot == null)
            {
                chipRoot = transform;
            }
        }
    }
}
