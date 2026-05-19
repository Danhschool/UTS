using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
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

        private bool isActive;
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
            button = GetComponent<Button>();
            rectTransform = GetComponent<RectTransform>();
            Disable();
        }

        public void EnableFor(BaseCommand command, IEnumerable<AbstractCommandable> selectedUnits, UnityAction onClick)
        {
            currentCommand = command;
            currentUnits = selectedUnits.ToArray();

            button.onClick.RemoveAllListeners();
            SetIcon(command.Icon);
            button.interactable = currentUnits.Any(unit =>
                command.IsAvailable(new CommandContext(unit, new RaycastHit())));
            button.onClick.AddListener(() =>
            {
                if (TryExecuteOrWarn(currentCommand, currentUnits))
                {
                    onClick.Invoke();
                }
            });
            isActive = true;

            if (tooltip != null)
            {
                tooltip.SetText(GetTooltipText(command));
            }
        }

        public void Disable()
        {
            currentCommand = null;
            currentUnits = System.Array.Empty<AbstractCommandable>();
            SetIcon(null);
            button.interactable = false;
            button.onClick.RemoveAllListeners();
            isActive = false;
            CancelInvoke();
        }

        /// <summary>
        /// Mục tiêu: Cho phép bấm nút khi thiếu tài nguyên để hiện cảnh báo thay vì im lặng.
        /// Cách hoạt động: Nếu chỉ thiếu supply thì Warn; nếu IsLocked vì tech/queue thì không gửi lệnh.
        /// </summary>
        private static bool TryExecuteOrWarn(BaseCommand command, AbstractCommandable[] units)
        {
            if (command == null || units.Length == 0)
            {
                return false;
            }

            SupplyCostSO cost = CommandSupplyCostUtility.TryGetCost(command);
            string actionDescription = CommandSupplyCostUtility.GetActionDescription(command);
            bool anyCanExecute = false;

            for (int i = 0; i < units.Length; i++)
            {
                CommandContext context = new(units[i], new RaycastHit());
                if (!command.IsAvailable(context))
                {
                    continue;
                }

                if (cost != null && !SupplyAffordability.HasEnough(context.Owner, cost))
                {
                    SupplyAffordability.WarnPlayerIfInsufficient(context.Owner, cost, actionDescription);
                    continue;
                }

                if (!command.IsLocked(context))
                {
                    anyCanExecute = true;
                }
            }

            return anyCanExecute;
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

        private void SetIcon(Sprite icon)
        {
            if (icon == null)
            {
                this.icon.enabled = false;
            }
            else
            {
                this.icon.sprite = icon;
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

                if (!SupplyAffordability.HasEnough(Owner.Player1, supplyCost))
                {
                    tooltipText += "\n<color=#FF8800>Không đủ tài nguyên!</color>";
                }
            }

            if (command.IsLocked(new CommandContext(Owner.Player1, null, new RaycastHit()))
                && command is IUnlockableCommand unlockableCommand)
            {
                UnlockableSO[] dependencies = unlockableCommand.GetUnmetDependencies(Owner.Player1);

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
