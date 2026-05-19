using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// One chat row prefab; bound when instantiated by <see cref="GameEventLogUI"/>.
    /// </summary>
    public class GameEventLogLineView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private float minLineHeight = 18f;
        [SerializeField] private float textVerticalPaddingPercent = 0.08f;

        private GameEventLogLine cachedLine;
        private bool cachedShowTimestamps;
        private bool hasCachedLine;

        private void Awake()
        {
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
        /// Mục tiêu: Hiển thị nội dung một dòng log và co chiều cao vừa khít text.
        /// Cách hoạt động: Gán TMP, đo preferred height theo chiều rộng vùng chat, set LayoutElement.minHeight.
        /// </summary>
        public void Bind(GameEventLogLine line, bool showTimestamps, float textAreaWidth)
        {
            if (messageText == null)
            {
                return;
            }

            cachedLine = line;
            cachedShowTimestamps = showTimestamps;
            hasCachedLine = true;

            messageText.richText = true;
            messageText.SetText(GameEventLogLineFormatter.Format(line, showTimestamps));
            ApplyPreferredHeight(textAreaWidth);
        }

        public void RefreshLayout(float textAreaWidth)
        {
            if (!hasCachedLine)
            {
                return;
            }

            ApplyPreferredHeight(textAreaWidth);
        }

        private void ApplyPreferredHeight(float textAreaWidth)
        {
            if (layoutElement == null || messageText == null)
            {
                return;
            }

            float width = textAreaWidth > 1f ? textAreaWidth : 280f;
            Vector2 preferred = messageText.GetPreferredValues(messageText.text, width, 0f);
            float verticalPad = Mathf.Max(2f, preferred.y * textVerticalPaddingPercent);
            layoutElement.minHeight = Mathf.Max(minLineHeight, preferred.y + verticalPad);
        }
    }
}
