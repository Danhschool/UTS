using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// One chat row prefab; height driven by LayoutElement preferred height for VerticalLayoutGroup.
    /// </summary>
    public class GameEventLogLineView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private float minLineHeight = 22f;
        [SerializeField] private float extraVerticalPadding = 4f;

        private void Awake()
        {
            rectTransform ??= transform as RectTransform;

            if (messageText == null)
            {
                messageText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }
        }

        /// <summary>
        /// Mục tiêu: Hiển thị một dòng chat với chiều cao đúng để VLG xếp chồng, không chồng text.
        /// Cách hoạt động: Đo TMP, gán preferredHeight + minHeight, cập nhật RectTransform chiều dọc.
        /// </summary>
        public void Bind(GameEventLogLine line, bool showTimestamps, float textAreaWidth)
        {
            if (messageText == null)
            {
                return;
            }

            messageText.richText = true;
            messageText.SetText(GameEventLogLineFormatter.Format(line, showTimestamps));
            ApplyLayoutHeight(textAreaWidth);
        }

        private void ApplyLayoutHeight(float textAreaWidth)
        {
            if (layoutElement == null || messageText == null || rectTransform == null)
            {
                return;
            }

            float width = textAreaWidth > 1f ? textAreaWidth : 280f;
            messageText.ForceMeshUpdate();
            Vector2 preferred = messageText.GetPreferredValues(messageText.text, width, 0f);
            float height = Mathf.Max(minLineHeight, preferred.y + extraVerticalPadding);

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
