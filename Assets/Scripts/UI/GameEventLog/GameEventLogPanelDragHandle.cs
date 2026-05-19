using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Kéo thanh chat tự do trong vùng canvas cha.
    /// </summary>
    public class GameEventLogPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private bool clampInsideParent = true;

        private RectTransform parentRect;
        private Vector2 dragPointerOffset;

        public void Initialize(RectTransform panelRect)
        {
            panel = panelRect;
            parentRect = panel.parent as RectTransform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (panel == null || parentRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            dragPointerOffset = panel.anchoredPosition - localPoint;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (panel == null || parentRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            panel.anchoredPosition = localPoint + dragPointerOffset;

            if (clampInsideParent)
            {
                ClampInsideParent();
            }
        }

        /// <summary>
        /// Mục tiêu: Giữ panel không bị kéo ra ngoài canvas.
        /// Cách hoạt động: Clamp anchoredPosition theo kích thước panel và parent (pivot dưới-trái).
        /// </summary>
        private void ClampInsideParent()
        {
            Vector2 panelSize = panel.rect.size;
            Vector2 parentSize = parentRect.rect.size;
            Vector2 position = panel.anchoredPosition;

            float maxX = Mathf.Max(0f, parentSize.x - panelSize.x);
            float maxY = Mathf.Max(0f, parentSize.y - panelSize.y);

            panel.anchoredPosition = new Vector2(
                Mathf.Clamp(position.x, 0f, maxX),
                Mathf.Clamp(position.y, 0f, maxY));
        }
    }
}
