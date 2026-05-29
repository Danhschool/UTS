using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Kéo panel UI tự do trong canvas cha (giống chat log).
    /// Gắn lên vùng kéo (title bar / header); gán Panel = khung cần di chuyển.
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeDraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] RectTransform panel;
        [SerializeField] bool clampInsideParent = true;

        RectTransform parentRect;
        Vector2 dragPointerOffset;

        void Awake()
        {
            if (panel == null)
            {
                panel = transform.parent as RectTransform;
            }

            parentRect = panel != null ? panel.parent as RectTransform : null;
        }

        /// <summary>
        /// Mục tiêu: gán panel từ code (GameEventLog, manual dialog, …).
        /// Cách hoạt động: lưu RectTransform và parent để tính toạ độ khi kéo.
        /// </summary>
        public void Initialize(RectTransform panelRect, bool clampToParent = true)
        {
            panel = panelRect;
            clampInsideParent = clampToParent;
            parentRect = panel != null ? panel.parent as RectTransform : null;
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

        void ClampInsideParent()
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
