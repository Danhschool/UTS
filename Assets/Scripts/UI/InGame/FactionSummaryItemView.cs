using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Một dòng trong scroll view tóm tắt phe (nhãn trái + current/total cố định cột phải).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryItemView : MonoBehaviour
    {
        [SerializeField] TMP_Text labelText;
        [SerializeField] TMP_Text valueText;
        [SerializeField] LayoutElement rowLayoutElement;
        [SerializeField] LayoutElement labelLayoutElement;
        [SerializeField] LayoutElement valueLayoutElement;

        [Header("Typography")]
        [SerializeField] float bodyFontSize = 18f;
        [SerializeField] float headerFontSize = 20f;
        [SerializeField] Color sectionHeaderColor = new(1f, 0.92f, 0.55f, 1f);
        [SerializeField] Color normalLabelColor = new(0.92f, 0.94f, 0.98f, 1f);
        [SerializeField] Color normalValueColor = new(0.75f, 0.92f, 1f, 1f);

        [Header("Layout")]
        [SerializeField] float dataRowHeight = 28f;
        [SerializeField] float headerRowHeight = 34f;
        [SerializeField] float valueColumnWidth = 112f;
        [SerializeField] float headerTopSpacing = 6f;

        bool layoutConfigured;

        void Awake()
        {
            ResolveReferences();
            EnsureFixedLayout();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị một dòng thống kê hoặc tiêu đề section với cột số thẳng hàng.
        /// Cách hoạt động: Cố định LayoutElement + TMP alignment; header ẩn cột value và tăng chiều cao dòng.
        /// </summary>
        public void SetData(string label, string value, bool isSectionHeader)
        {
            ResolveReferences();
            EnsureFixedLayout();

            if (rowLayoutElement != null)
            {
                rowLayoutElement.minHeight = isSectionHeader ? headerRowHeight : dataRowHeight;
                rowLayoutElement.preferredHeight = isSectionHeader ? headerRowHeight : dataRowHeight;
            }

            if (labelLayoutElement != null)
            {
                labelLayoutElement.flexibleWidth = 1f;
                labelLayoutElement.minWidth = 0f;
                labelLayoutElement.preferredWidth = -1f;
            }

            if (valueLayoutElement != null)
            {
                bool showValueColumn = !isSectionHeader;
                valueLayoutElement.minWidth = showValueColumn ? valueColumnWidth : 0f;
                valueLayoutElement.preferredWidth = showValueColumn ? valueColumnWidth : 0f;
                valueLayoutElement.flexibleWidth = 0f;
            }

            if (valueText != null)
            {
                valueText.gameObject.SetActive(!isSectionHeader);
            }

            if (labelText != null)
            {
                labelText.text = label;
                labelText.fontSize = isSectionHeader ? headerFontSize : bodyFontSize;
                labelText.fontStyle = isSectionHeader ? FontStyles.Bold : FontStyles.Normal;
                labelText.color = isSectionHeader ? sectionHeaderColor : normalLabelColor;
                labelText.margin = isSectionHeader
                    ? new Vector4(0f, headerTopSpacing, 0f, 0f)
                    : Vector4.zero;
            }

            if (valueText != null && !isSectionHeader)
            {
                valueText.text = value;
                valueText.fontSize = bodyFontSize;
                valueText.fontStyle = FontStyles.Normal;
                valueText.color = normalValueColor;
            }

            if (transform is RectTransform rowRect)
            {
                LayoutRebuilder.MarkLayoutForRebuild(rowRect);
            }
        }

        void EnsureFixedLayout()
        {
            if (layoutConfigured)
            {
                return;
            }

            layoutConfigured = true;

            rowLayoutElement ??= GetComponent<LayoutElement>();
            if (rowLayoutElement == null)
            {
                rowLayoutElement = gameObject.AddComponent<LayoutElement>();
            }

            if (labelText != null)
            {
                labelLayoutElement ??= labelText.GetComponent<LayoutElement>();
                if (labelLayoutElement == null)
                {
                    labelLayoutElement = labelText.gameObject.AddComponent<LayoutElement>();
                }

                PrepareRectForLayoutGroup(labelText.rectTransform);
                ConfigureText(
                    labelText,
                    bodyFontSize,
                    TextAlignmentOptions.MidlineLeft,
                    TextOverflowModes.Ellipsis);
            }

            if (valueText != null)
            {
                valueLayoutElement ??= valueText.GetComponent<LayoutElement>();
                if (valueLayoutElement == null)
                {
                    valueLayoutElement = valueText.gameObject.AddComponent<LayoutElement>();
                }

                PrepareRectForLayoutGroup(valueText.rectTransform);
                ConfigureText(
                    valueText,
                    bodyFontSize,
                    TextAlignmentOptions.MidlineRight,
                    TextOverflowModes.Overflow);
            }
        }

        static void PrepareRectForLayoutGroup(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        static void ConfigureText(
            TMP_Text text,
            float fontSize,
            TextAlignmentOptions alignment,
            TextOverflowModes overflowMode)
        {
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = overflowMode;
            text.richText = false;
        }

        void ResolveReferences()
        {
            if (labelText != null && valueText != null)
            {
                return;
            }

            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length == 0)
            {
                return;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text candidate = labels[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.gameObject.name.Contains("Value"))
                {
                    valueText ??= candidate;
                }
                else
                {
                    labelText ??= candidate;
                }
            }

            labelText ??= labels[0];
            if (labels.Length > 1 && valueText == null)
            {
                valueText = labels[1];
            }
        }
    }
}
