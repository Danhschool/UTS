using System.Collections;
using System.Text;
using GameDevTV.RTS.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Game event log: append text + ScrollRect/Viewport/Mask (không tràn viền, cuộn được).
    /// </summary>
    public class GameEventLogUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private TextMeshProUGUI chatHistoryTmp;
        [SerializeField] private Text chatHistoryText;
        [SerializeField] private RectTransform dragHandle;

        [Header("Display")]
        [SerializeField] private bool showTimestamps = true;
        [SerializeField] private bool autoScrollToBottom = true;

        [Header("Panel interaction")]
        [SerializeField] private bool allowFreeMove = true;

        private readonly StringBuilder stringBuilder = new(2048);
        private FreeDraggablePanel dragBehaviour;
        private Coroutine scrollRoutine;

        private void Awake()
        {
            panelRect ??= transform as RectTransform;
            scrollRect ??= GetComponentInChildren<ScrollRect>(true);
            if (contentRoot == null && scrollRect != null)
            {
                contentRoot = scrollRect.content;
            }

            SetupPanelInteraction();
        }

        private void OnEnable()
        {
            GameEventLog.LineAdded += HandleLineAdded;
            RebuildHistory();
        }

        private void OnDisable()
        {
            GameEventLog.LineAdded -= HandleLineAdded;
        }

        private void SetupPanelInteraction()
        {
            if (panelRect == null)
            {
                return;
            }

            if (allowFreeMove && dragHandle != null)
            {
                dragBehaviour = dragHandle.GetComponent<FreeDraggablePanel>();
                if (dragBehaviour == null)
                {
                    dragBehaviour = dragHandle.gameObject.AddComponent<FreeDraggablePanel>();
                }

                dragBehaviour.Initialize(panelRect, clampToParent: true);
            }
        }

        private void HandleLineAdded(GameEventLogLine line)
        {
            AppendLine(line);
            RefreshContentHeight();

            if (autoScrollToBottom)
            {
                RequestScrollToBottom();
            }
        }

        private void RebuildHistory()
        {
            stringBuilder.Clear();

            foreach (GameEventLogLine line in GameEventLog.Lines)
            {
                stringBuilder.Append(GameEventLogLineFormatter.Format(line, showTimestamps));
                stringBuilder.Append('\n');
            }

            ApplyHistoryText(stringBuilder.ToString());
            RefreshContentHeight();

            if (autoScrollToBottom)
            {
                RequestScrollToBottom();
            }
        }

        private void AppendLine(GameEventLogLine line)
        {
            string lineText = GameEventLogLineFormatter.Format(line, showTimestamps) + "\n";

            if (chatHistoryTmp != null)
            {
                chatHistoryTmp.text += lineText;
                return;
            }

            if (chatHistoryText != null)
            {
                chatHistoryText.text += lineText;
            }
        }

        private void ApplyHistoryText(string text)
        {
            if (chatHistoryTmp != null)
            {
                chatHistoryTmp.text = text;
                return;
            }

            if (chatHistoryText != null)
            {
                chatHistoryText.text = text;
            }
        }

        /// <summary>
        /// Mục tiêu: Content cao đúng bằng nội dung TMP để ScrollRect cuộn được.
        /// Cách hoạt động: Đo preferred height theo chiều rộng viewport, gán size Content và TMP.
        /// </summary>
        private void RefreshContentHeight()
        {
            if (contentRoot == null)
            {
                return;
            }

            float width = GetTextAreaWidth();
            float textHeight = MeasureTextHeight(width);
            float minHeight = GetViewportHeight();

            float contentHeight = Mathf.Max(minHeight, textHeight + 8f);
            contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            if (chatHistoryTmp != null)
            {
                RectTransform textRect = chatHistoryTmp.rectTransform;
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        private float MeasureTextHeight(float width)
        {
            if (chatHistoryTmp != null)
            {
                chatHistoryTmp.ForceMeshUpdate();
                return chatHistoryTmp.GetPreferredValues(chatHistoryTmp.text, width, 0f).y;
            }

            if (chatHistoryText != null)
            {
                return chatHistoryText.preferredHeight;
            }

            return minLineHeightFallback;
        }

        private const float minLineHeightFallback = 22f;

        private float GetTextAreaWidth()
        {
            float width = contentRoot != null ? contentRoot.rect.width : 0f;

            if (width <= 1f && scrollRect != null && scrollRect.viewport != null)
            {
                width = scrollRect.viewport.rect.width;
            }

            return Mathf.Max(80f, width - 16f);
        }

        private float GetViewportHeight()
        {
            if (scrollRect != null && scrollRect.viewport != null)
            {
                return scrollRect.viewport.rect.height;
            }

            return 100f;
        }

        private void RequestScrollToBottom()
        {
            if (!autoScrollToBottom || scrollRect == null || !isActiveAndEnabled)
            {
                return;
            }

            if (scrollRoutine != null)
            {
                StopCoroutine(scrollRoutine);
            }

            scrollRoutine = StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            RefreshContentHeight();

            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            scrollRect.verticalNormalizedPosition = 0f;

            scrollRoutine = null;
        }

        public void ClearDisplay()
        {
            GameEventLog.Clear();
            ApplyHistoryText(string.Empty);
            RefreshContentHeight();
        }
    }
}
