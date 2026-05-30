using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIActionButton : MonoBehaviour, IUIElement<BaseCommand, IEnumerable<AbstractCommandable>, UnityAction>, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Tooltip tooltip;
        [SerializeField] private TextMeshProUGUI slotNumberLabel;
        [SerializeField] private Image buttonBackground;

        [Header("Button tint (Target Graphic — không tô icon)")]
        [SerializeField] private Color normalButtonColor = new(1f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color highlightedButtonColor = new(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color pressedButtonColor = new(0.8f, 0.65f, 0.1f, 1f);
        [SerializeField] private Color selectedButtonColor = new(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color disabledButtonColor = new(0.55f, 0.55f, 0.55f, 0.45f);

        [Header("Khi lệnh đang được chọn (chờ click map)")]
        [SerializeField] private Color pendingNormalColor = Color.white;
        [SerializeField] private Color pendingHighlightedColor = new(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color pendingPressedColor = new(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color pendingSelectedColor = Color.white;

        [Header("Slot label (phím 1–9)")]
        [SerializeField] private Color slotNumberColor = new(0.12f, 0.12f, 0.12f, 1f);

        private bool isActive;
        private bool isCommandPending;
        private RectTransform rectTransform;
        private Button button;
        private BaseCommand currentCommand;
        private AbstractCommandable[] currentUnits = System.Array.Empty<AbstractCommandable>();

        private static readonly string STONE_FORMAT = "{0} <color=#B0B0B0>Stone</color>. ";
        private static readonly string WOOD_FORMAT = "{0} <color=#8B5A2B>Wood</color>. ";
        private static readonly string FOOD_FORMAT = "{0} <color=#E6A817>Food</color>. ";
        private static readonly string DEPENDENCY_FORMAT_NO_COMMA = "<color=#AC0000>{0}</color>.";
        private static readonly string DEPENDENCY_FORMAT_COMMA = "<color=#AC0000>{0}</color>, ";

        private void Awake()
        {
            EnsureComponentsCached();
            ApplyButtonBackgroundColors();
            Disable();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị số phím tương ứng slot (0 → "1", …, 8 → "9").
        /// Cách hoạt động: Lưu index và cập nhật TMP trên child "number" nếu có.
        /// </summary>
        public void SetDisplaySlot(int slotIndex)
        {
            EnsureComponentsCached();

            if (slotNumberLabel == null)
            {
                return;
            }

            slotNumberLabel.text = (slotIndex + 1).ToString();
            slotNumberLabel.color = slotNumberColor;
            slotNumberLabel.gameObject.SetActive(true);
        }

        void EnsureComponentsCached()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (buttonBackground == null)
            {
                buttonBackground = GetComponent<Image>();
            }

            if (slotNumberLabel == null)
            {
                Transform numberChild = transform.Find("number");
                if (numberChild != null)
                {
                    slotNumberLabel = numberChild.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Nền nút vàng qua ColorBlock của Button, icon giữ màu gốc.
        /// Cách hoạt động: Gán m_TargetGraphic = buttonBackground; đặt ColorBlock vàng; icon.color = trắng.
        /// </summary>
        /// <summary>
        /// Mục tiêu: Nút trắng khi lệnh trên nút này đang chờ xác nhận trên map.
        /// Cách hoạt động: So reference BaseCommand với lệnh pending từ bus → bật isCommandPending → áp ColorBlock trắng.
        /// </summary>
        public void RefreshPendingHighlight(BaseCommand pendingCommand)
        {
            isCommandPending = isActive
                && pendingCommand != null
                && currentCommand != null
                && pendingCommand == currentCommand;
            ApplyButtonBackgroundColors();
        }

        void ApplyButtonBackgroundColors()
        {
            if (button == null || buttonBackground == null)
            {
                return;
            }

            button.targetGraphic = buttonBackground;
            buttonBackground.color = Color.white;

            ColorBlock colors = button.colors;
            if (isCommandPending)
            {
                colors.normalColor = pendingNormalColor;
                colors.highlightedColor = pendingHighlightedColor;
                colors.pressedColor = pendingPressedColor;
                colors.selectedColor = pendingSelectedColor;
            }
            else
            {
                colors.normalColor = normalButtonColor;
                colors.highlightedColor = highlightedButtonColor;
                colors.pressedColor = pressedButtonColor;
                colors.selectedColor = selectedButtonColor;
            }

            colors.disabledColor = disabledButtonColor;
            button.colors = colors;
        }

        public void EnableFor(BaseCommand command, IEnumerable<AbstractCommandable> selectedUnits, UnityAction onClick)
        {
            EnsureComponentsCached();
            if (button == null)
            {
                return;
            }

            currentCommand = command;
            currentUnits = selectedUnits.ToArray();

            button.onClick.RemoveAllListeners();
            SetIcon(command.Icon);
            button.interactable = currentUnits.Any(unit =>
                command.IsAvailable(new CommandContext(unit, new RaycastHit())));
            button.onClick.AddListener(() =>
            {
                if (ActionBarCommandExecution.TryValidateForExecution(currentCommand, currentUnits))
                {
                    onClick.Invoke();
                }
            });
            isActive = true;
            ApplyButtonBackgroundColors();

            if (tooltip != null)
            {
                tooltip.SetText(GetTooltipText(command));
            }
        }

        public void Disable()
        {
            EnsureComponentsCached();
            currentCommand = null;
            currentUnits = System.Array.Empty<AbstractCommandable>();
            SetIcon(null);

            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            isCommandPending = false;
            isActive = false;
            ApplyButtonBackgroundColors();
            CancelInvoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActive)
            {
                Invoke(nameof(ShowTooltip), tooltip.HoverDelay);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
            CancelInvoke();
        }

        private void ShowTooltip()
        {
            if (tooltip != null)
            {
                tooltip.Show();
                tooltip.RectTransform.position = new Vector2(
                    rectTransform.position.x + rectTransform.rect.width / 2f,
                    rectTransform.position.y + rectTransform.rect.height / 2f
                );
            }
        }

        private void SetIcon(Sprite iconSprite)
        {
            if (this.icon == null)
            {
                return;
            }

            if (iconSprite == null)
            {
                this.icon.enabled = false;
            }
            else
            {
                this.icon.sprite = iconSprite;
                this.icon.color = Color.white;
                this.icon.enabled = true;
            }
        }

        private string GetTooltipText(BaseCommand command)
        {
            string tooltipText = command.Name + "\n";

            SupplyCostSO supplyCost = CommandSupplyCostUtility.TryGetCost(command);

            if (supplyCost != null)
            {
                if (supplyCost.Stone > 0)
                {
                    tooltipText += string.Format(STONE_FORMAT, supplyCost.Stone);
                }

                if (supplyCost.Wood > 0)
                {
                    tooltipText += string.Format(WOOD_FORMAT, supplyCost.Wood);
                }

                if (supplyCost.Food > 0)
                {
                    tooltipText += string.Format(FOOD_FORMAT, supplyCost.Food);
                }

                Owner hudOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
                if (!SupplyAffordability.HasEnough(hudOwner, supplyCost))
                {
                    tooltipText += "\n<color=#FF8800>Không đủ tài nguyên!</color>";
                }
            }

            Owner tooltipOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (command.IsLocked(new CommandContext(tooltipOwner, null, new RaycastHit()))
                && command is IUnlockableCommand unlockableCommand)
            {
                UnlockableSO[] dependencies = unlockableCommand.GetUnmetDependencies(tooltipOwner);

                if (dependencies.Length > 0)
                {
                    tooltipText += "\nRequires: ";
                }

                for(int i = 0; i < dependencies.Length; i++)
                {
                    tooltipText += i == dependencies.Length - 1
                        ? string.Format(DEPENDENCY_FORMAT_NO_COMMA, dependencies[i].Name)
                        : string.Format(DEPENDENCY_FORMAT_COMMA, dependencies[i].Name);
                }
            }

            return tooltipText;
        }
    }
}
