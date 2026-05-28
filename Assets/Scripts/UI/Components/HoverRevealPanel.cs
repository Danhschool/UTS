using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Hover vào object này → hiện panel; rời chuột → tắt panel.
    /// Gắn cùng GameObject có Image/Button (raycastTarget bật).
    /// </summary>
    [DisallowMultipleComponent]
    public class HoverRevealPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] GameObject panel;
        [SerializeField, Min(0f)] float showDelay;

        bool isPointerOver;

        void Awake()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        void OnDisable()
        {
            CancelInvoke(nameof(ShowPanel));
            isPointerOver = false;
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
            HidePanel();
        }

        void ShowPanel()
        {
            if (!isPointerOver || panel == null)
            {
                return;
            }

            panel.SetActive(true);
        }

        void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    }
}
