using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Một ô loại unit trong panel chọn nhiều unit (icon + số lượng).
    /// </summary>
    public class MultiUnitTypeSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;

        private void Awake()
        {
            if (icon == null)
            {
                Transform iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                {
                    icon = iconTransform.GetComponent<Image>();
                }
            }

            if (countText == null)
            {
                Transform countTransform = transform.Find("Unit Count Text");
                if (countTransform != null)
                {
                    countText = countTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Hiển thị icon loại unit và số lượng đang chọn.
        /// Cách hoạt động: Gán sprite icon; count > 1 thì hiện dạng ×N trên countText.
        /// </summary>
        public void Set(Sprite iconSprite, int count)
        {
            gameObject.SetActive(true);

            if (icon != null && iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = true;
            }

            if (countText != null)
            {
                countText.SetText(count > 1 ? $"×{count}" : count.ToString());
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
