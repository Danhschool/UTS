using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Hover vào object này → hiện panel; rời chuột → tắt panel.
    /// Gắn cùng GameObject có Image/Button (raycastTarget bật).
    /// </summary>
    [DisallowMultipleComponent]
    public class HoverRevealPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] GameObject panel;
        [SerializeField, Min(0f)] float showDelay;
        [SerializeField] bool lockRevealAfterClick;

        bool isPointerOver;
        bool isLockedVisible;

        void Awake()
        {
            if (panel != null && !IsPanelAncestorOfThis(panel.transform))
            {
                panel.SetActive(false);
            }
        }

        void OnDisable()
        {
            CancelInvoke(nameof(ShowPanel));
            isPointerOver = false;
            isLockedVisible = false;
            HidePanel();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;

            if (panel == null)
            {
                return;
            }

            CancelInvoke(nameof(ShowPanel));

            if (showDelay <= 0f)
            {
                ShowPanel();
            }
            else
            {
                Invoke(nameof(ShowPanel), showDelay);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            CancelInvoke(nameof(ShowPanel));
            if (!isLockedVisible)
            {
                HidePanel();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!lockRevealAfterClick || panel == null)
            {
                return;
            }

            isLockedVisible = true;
            ShowPanelForce();
        }

        void ShowPanel()
        {
            if (!isPointerOver || panel == null)
            {
                return;
            }

            panel.SetActive(true);
        }

        /// <summary>
        /// Mục tiêu: Bật panel ngay tại thời điểm click để giữ trạng thái "đã chọn".
        /// Cách hoạt động: Bỏ điều kiện hover hiện tại, chỉ cần panel tồn tại là SetActive(true).
        /// </summary>
        void ShowPanelForce()
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        /// <summary>
        /// Mục tiêu: Cho phép script ngoài bỏ trạng thái khóa hover của tab.
        /// Cách hoạt động: Hạ cờ lock; nếu chuột không còn ở trên thì ẩn panel ngay.
        /// </summary>
        public void ClearLock()
        {
            isLockedVisible = false;
            if (!isPointerOver)
            {
                HidePanel();
            }
        }

        /// <summary>
        /// Mục tiêu: Cho UI controller cấu hình nút có giữ hover sau click hay không.
        /// Cách hoạt động: Bật/tắt lockRevealAfterClick; khi tắt sẽ xóa luôn lock hiện tại.
        /// </summary>
        public void SetLockRevealAfterClick(bool enabled)
        {
            lockRevealAfterClick = enabled;
            if (!enabled)
            {
                ClearLock();
            }
        }

        void HidePanel()
        {
            if (panel == null || IsPanelAncestorOfThis(panel.transform))
            {
                return;
            }

            panel.SetActive(false);
        }

        /// <summary>
        /// Mục tiêu: Không tắt panel cha khi HoverReveal nằm bên trong panel vừa được bật lần đầu.
        /// Cách hoạt động: Kiểm tra panel có phải ancestor của transform hiện tại.
        /// </summary>
        bool IsPanelAncestorOfThis(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return false;
            }

            Transform current = transform;
            while (current != null)
            {
                if (current == panelTransform)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
