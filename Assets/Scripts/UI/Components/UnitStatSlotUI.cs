using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Một ô stat (prefab Armor Damage Icon): level upgrade, giá trị, icon, tooltip.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UnitStatSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Prefab: Armor Damage Icon")]
        [SerializeField] private TextMeshProUGUI upgradeLevelText;
        [SerializeField] private TextMeshProUGUI statValueText;
        [SerializeField] private Image statIconImage;

        [Header("Tooltip")]
        [SerializeField] private Tooltip tooltip;
        [TextArea(2, 4)]
        [Tooltip("{0} = giá trị stat (vd. 5 hoặc 2.5/s), {1} = số upgrade đã research. Ví dụ: Tốc độ đánh: {0}")]
        [SerializeField] private string tooltipDescription;

        private RectTransform rectTransform;
        private Sprite initialIconSprite;
        private bool isActive;
        private string tooltipStatValue;
        private int tooltipUpgradeLevel;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (statIconImage != null)
            {
                initialIconSprite = statIconImage.sprite;
            }

            ApplyTooltipText();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị giá trị stat và số level upgrade đã nghiên cứu.
        /// Cách hoạt động: Gán text/icon; tooltip dùng tooltipDescription với {0}/{1} nếu có placeholder.
        /// </summary>
        /// <param name="tooltipValue">Text cho {0} trong tooltip; để trống thì dùng statValue.</param>
        public void Set(string statValue, int upgradeLevel, Sprite iconOverride = null, string tooltipValue = null)
        {
            gameObject.SetActive(true);
            isActive = true;
            tooltipStatValue = string.IsNullOrEmpty(tooltipValue) ? statValue : tooltipValue;
            tooltipUpgradeLevel = upgradeLevel;

            if (upgradeLevelText != null)
            {
                upgradeLevelText.SetText(upgradeLevel.ToString());
            }

            if (statValueText != null)
            {
                statValueText.SetText(statValue);
            }

            if (statIconImage != null)
            {
                if (iconOverride != null)
                {
                    statIconImage.sprite = iconOverride;
                }
                else if (initialIconSprite != null)
                {
                    statIconImage.sprite = initialIconSprite;
                }
            }

            ApplyTooltipText();
        }

        public void Hide()
        {
            isActive = false;
            CancelInvoke(nameof(ShowTooltip));
            tooltip?.Hide();
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActive || tooltip == null)
            {
                return;
            }

            Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelInvoke(nameof(ShowTooltip));
            tooltip?.Hide();
        }

        private void ShowTooltip()
        {
            if (!isActive || tooltip == null)
            {
                return;
            }

            ApplyTooltipText();
            tooltip.Show();
            tooltip.RectTransform.position = new Vector2(
                rectTransform.position.x + rectTransform.rect.width * 0.5f,
                rectTransform.position.y + rectTransform.rect.height * 0.5f);
        }

        private void ApplyTooltipText()
        {
            if (tooltip == null || string.IsNullOrWhiteSpace(tooltipDescription))
            {
                return;
            }

            string text = tooltipDescription.Contains("{0}") || tooltipDescription.Contains("{1}")
                ? string.Format(tooltipDescription, tooltipStatValue, tooltipUpgradeLevel)
                : tooltipDescription;
            tooltip.SetText(text);
        }
    }
}
