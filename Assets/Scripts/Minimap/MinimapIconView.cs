using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapIconView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        public void Configure(MinimapMarkerPresentation presentation)
        {
            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = presentation.Sprite;
            iconImage.color = presentation.Color;
            iconImage.preserveAspect = presentation.IsUnitIcon;
            iconImage.enabled = presentation.Sprite != null;

            if (transform is RectTransform rectTransform)
            {
                rectTransform.sizeDelta = presentation.Size;
            }
        }

        /// <summary>
        /// Mục tiêu: Đặt icon tại vị trí trên minimap (UV 0–1 trong parent stretch).
        /// Cách hoạt động: anchorMin = anchorMax = uv; anchoredPosition = 0.
        /// </summary>
        public void SetNormalizedPosition(Vector2 normalized)
        {
            if (transform is not RectTransform rectTransform)
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
