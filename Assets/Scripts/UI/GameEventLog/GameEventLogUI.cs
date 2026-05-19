using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Scrollable chat panel; each log line is an instantiated <see cref="GameEventLogLineView"/> prefab.
    /// Supports free drag, resize, and padding/spacing scaled to panel size.
    /// </summary>
    public class GameEventLogUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private GameEventLogLineView linePrefab;
        [SerializeField] private RectTransform dragHandle;
        [SerializeField] private RectTransform resizeHandle;

        [Header("Display")]
        [SerializeField] private bool showTimestamps;
        [SerializeField] private bool autoScrollToBottom = true;

        [Header("Panel interaction")]
        [SerializeField] private bool allowFreeMove = true;
        [SerializeField] private bool allowResize = true;

        [Header("Flexible layout (% of panel size)")]
        [SerializeField] private float horizontalPaddingPercent = 0.035f;
        [SerializeField] private float verticalPaddingPercent = 0.04f;
        [SerializeField] private float lineSpacingPercent = 0.018f;
        [SerializeField] private float minPadding = 4f;
        [SerializeField] private float maxPadding = 18f;
        [SerializeField] private float minLineSpacing = 3f;
        [SerializeField] private float maxLineSpacing = 10f;

        private readonly List<GameEventLogLineView> activeLines = new();
        private VerticalLayoutGroup contentLayout;
        private Vector2 lastPanelSize;
        private GameEventLogPanelDragHandle dragBehaviour;
        private GameEventLogPanelResizeHandle resizeBehaviour;

        private void Awake()
        {
            panelRect ??= transform as RectTransform;
            contentLayout ??= contentRoot != null ? contentRoot.GetComponent<VerticalLayoutGroup>() : null;

            SetupPanelInteraction();
            ApplyFlexibleLayout();
        }

        private void LateUpdate()
        {
            if (panelRect == null)
            {
                return;
            }

            Vector2 size = panelRect.rect.size;
            if (size != lastPanelSize)
            {
                lastPanelSize = size;
                ApplyFlexibleLayout();
            }
        }

        private void OnEnable()
        {
            GameEventLog.LineAdded += HandleLineAdded;
            RebuildAllLines();
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
                dragBehaviour = dragHandle.GetComponent<GameEventLogPanelDragHandle>();
                if (dragBehaviour == null)
                {
                    dragBehaviour = dragHandle.gameObject.AddComponent<GameEventLogPanelDragHandle>();
                }

                dragBehaviour.Initialize(panelRect);
            }

            if (allowResize && resizeHandle != null)
            {
                resizeBehaviour = resizeHandle.GetComponent<GameEventLogPanelResizeHandle>();
                if (resizeBehaviour == null)
                {
                    resizeBehaviour = resizeHandle.gameObject.AddComponent<GameEventLogPanelResizeHandle>();
                }

                resizeBehaviour.Initialize(panelRect, ApplyFlexibleLayout);
            }
        }

        /// <summary>
        /// Mục tiêu: Padding và khoảng cách dòng scale theo kích thước khung chat.
        /// Cách hoạt động: Tính % width/height của panel, clamp min/max, gán vào VerticalLayoutGroup và refresh dòng.
        /// </summary>
        private void ApplyFlexibleLayout()
        {
            if (panelRect == null || contentLayout == null)
            {
                return;
            }

            float width = panelRect.rect.width;
            float height = panelRect.rect.height;

            int padX = Mathf.RoundToInt(Mathf.Clamp(width * horizontalPaddingPercent, minPadding, maxPadding));
            int padY = Mathf.RoundToInt(Mathf.Clamp(height * verticalPaddingPercent, minPadding, maxPadding));

            contentLayout.padding = new RectOffset(padX, padX, padY, padY);
            contentLayout.spacing = Mathf.Clamp(height * lineSpacingPercent, minLineSpacing, maxLineSpacing);

            RefreshLineLayouts(width - padX * 2f);
        }

        private void HandleLineAdded(GameEventLogLine line)
        {
            if (linePrefab == null || contentRoot == null)
            {
                return;
            }

            SpawnLine(line);
            TrimExcessLines();
            ScrollToBottom();
        }

        private void RebuildAllLines()
        {
            ClearLineViews();

            if (linePrefab == null || contentRoot == null)
            {
                return;
            }

            foreach (GameEventLogLine line in GameEventLog.Lines)
            {
                SpawnLine(line);
            }

            ScrollToBottom();
        }

        private void SpawnLine(GameEventLogLine line)
        {
            GameEventLogLineView view = Instantiate(linePrefab, contentRoot);
            float textWidth = GetTextAreaWidth();
            view.Bind(line, showTimestamps, textWidth);
            activeLines.Add(view);
        }

        private void RefreshLineLayouts(float textAreaWidth)
        {
            for (int i = 0; i < activeLines.Count; i++)
            {
                activeLines[i]?.RefreshLayout(textAreaWidth);
            }

            if (contentRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
        }

        private float GetTextAreaWidth()
        {
            if (panelRect == null || contentLayout == null)
            {
                return 280f;
            }

            float width = panelRect.rect.width;
            float pad = contentLayout.padding.left + contentLayout.padding.right;
            return Mathf.Max(80f, width - pad);
        }

        private void TrimExcessLines()
        {
            while (activeLines.Count > GameEventLog.MaxLines)
            {
                GameEventLogLineView oldest = activeLines[0];
                activeLines.RemoveAt(0);

                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }
        }

        private void ScrollToBottom()
        {
            if (!autoScrollToBottom || scrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void ClearLineViews()
        {
            for (int i = 0; i < activeLines.Count; i++)
            {
                if (activeLines[i] != null)
                {
                    Destroy(activeLines[i].gameObject);
                }
            }

            activeLines.Clear();
        }

        public void ClearDisplay()
        {
            GameEventLog.Clear();
            ClearLineViews();
        }
    }
}
