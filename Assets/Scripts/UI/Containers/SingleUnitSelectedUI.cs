using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class SingleUnitSelectedUI : MonoBehaviour, IUIElement<AbstractCommandable>
    {
        [SerializeField] private TextMeshProUGUI unitName;
        [SerializeField] private UnitStatsPanelUI statsPanel;

        private void Awake()
        {
            if (statsPanel == null)
            {
                statsPanel = GetComponent<UnitStatsPanelUI>();
            }
        }

        public void EnableFor(AbstractCommandable item)
        {
            gameObject.SetActive(true);
            unitName.SetText(item.UnitSO.Name);
            statsPanel?.Bind(item);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
            statsPanel?.HideAllSlots();
        }
    }
}