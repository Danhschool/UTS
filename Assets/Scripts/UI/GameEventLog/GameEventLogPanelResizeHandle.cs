using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Kéo góc để đổi kích thước khung chat (panel pivot dưới-trái).
    /// </summary>
    public class GameEventLogPanelResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Vector2 minSize = new(200f, 120f);
        [SerializeField] private Vector2 maxSize = new(560f, 520f);

        private RectTransform parentRect;
        private Vector2 dragStartLocalPoint;
        private Vector2 dragStartPanelSize;
        private System.Action onResized;

        public void Initialize(RectTransform panelRect, System.Action resizedCallback)
        {
            panel = panelRect;
            parentRect = panel.parent as RectTransform;
            onResized = resizedCallback;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (panel == null || parentRect == null)
            {
                return;
            }

            dragStartPanelSize = panel.sizeDelta;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out dragStartLocalPoint);
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

            Vector2 delta = localPoint - dragStartLocalPoint;
            panel.sizeDelta = new Vector2(
                Mathf.Clamp(dragStartPanelSize.x + delta.x, minSize.x, maxSize.x),
                Mathf.Clamp(dragStartPanelSize.y + delta.y, minSize.y, maxSize.y));

            onResized?.Invoke();
        }
    }
}
