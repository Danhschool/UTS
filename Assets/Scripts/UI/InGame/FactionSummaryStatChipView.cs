using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Một ô thống kê trong hàng ngang — "Đá 450/500".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryStatChipView : MonoBehaviour
    {
        [SerializeField] TMP_Text statText;
        [SerializeField] float fontSize = 20f;
        [SerializeField] Color labelColor = new(0.92f, 0.94f, 0.98f, 1f);
        [SerializeField] Color valueColor = new(0.75f, 0.92f, 1f, 1f);
        [SerializeField] float minChipWidth = 140f;

        void Awake()
        {
            ResolveReferences();
            EnsureLayout();
            HideLegacyValueColumn();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị một chỉ số dạng "Nhãn current/total" trên một hàng ngang.
        /// Cách hoạt động: Rich text nhẹ — nhãn trắng, số xanh nhạt; font size 20.
        /// </summary>
        public void SetData(string label, int current, int total)
        {
            ResolveReferences();
            EnsureLayout();

            if (statText == null)
            {
                return;
            }

            statText.fontSize = fontSize;
            statText.richText = true;
            statText.text =
                $"<color=#{ColorUtility.ToHtmlStringRGB(labelColor)}>{label}</color> " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(valueColor)}>{current}/{total}</color>";
        }

        void EnsureLayout()
        {
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.minWidth = minChipWidth;
            layout.preferredWidth = minChipWidth;
            layout.flexibleWidth = 1f;
            layout.minHeight = 28f;
            layout.preferredHeight = 28f;

            if (statText != null)
            {
                RectTransform rect = statText.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                statText.alignment = TextAlignmentOptions.MidlineLeft;
                statText.textWrappingMode = TextWrappingModes.NoWrap;
                statText.overflowMode = TextOverflowModes.Overflow;
            }
        }

        void ResolveReferences()
        {
            statText ??= GetComponentInChildren<TMP_Text>(true);
        }

        void HideLegacyValueColumn()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];
                if (candidate == null || candidate == statText)
                {
                    continue;
                }

                if (candidate.gameObject.name.Contains("Value"))
                {
                    candidate.gameObject.SetActive(false);
                }
            }
        }
    }
}
