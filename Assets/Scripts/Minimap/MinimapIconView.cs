using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapIconView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        public void Configure(Sprite sprite, Color color, Vector2 size)
        {
            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            if (iconImage != null)
            {
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                }

                iconImage.color = color;
            }

            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = size;
            }
        }

        /// <summary>
        /// Mục tiêu: Đặt icon tại vị trí trên minimap (UV 0–1 trong parent stretch).
        /// Cách hoạt động: anchorMin = anchorMax = uv; anchoredPosition = 0.
        /// </summary>
        public void SetNormalizedPosition(Vector2 normalized)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = normalized;
            rectTransform.anchorMax = normalized;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
