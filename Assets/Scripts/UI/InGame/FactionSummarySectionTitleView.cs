using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Tiêu đề section (Tài nguyên, Đơn vị, …) trong panel tóm tắt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummarySectionTitleView : MonoBehaviour
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] float fontSize = 20f;
        [SerializeField] Color titleColor = new(1f, 0.92f, 0.55f, 1f);
        [SerializeField] float rowHeight = 30f;

        void Awake()
        {
            ResolveReferences();
            ApplyTypography();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị tiêu đề nhóm thống kê.
        /// Cách hoạt động: Gán TMP + chiều cao dòng cố định cho VerticalLayoutGroup.
        /// </summary>
        public void SetTitle(string title)
        {
            ResolveReferences();
            ApplyTypography();

            if (titleText != null)
            {
                titleText.text = title;
            }

            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.minHeight = rowHeight;
            layout.preferredHeight = rowHeight;
        }

        void ApplyTypography()
        {
            if (titleText == null)
            {
                return;
            }

            titleText.fontSize = fontSize;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = titleColor;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void ResolveReferences()
        {
            titleText ??= GetComponentInChildren<TMP_Text>(true);
        }
    }
}
