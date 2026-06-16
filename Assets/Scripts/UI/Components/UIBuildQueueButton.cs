using GameDevTV.RTS.Units;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using GameDevTV.RTS.TechTree;

namespace GameDevTV.RTS.UI.Components
{
    public class UIBuildQueueButton : MonoBehaviour, IUIElement<UnlockableSO, UnityAction>
    {
        [SerializeField] private Image icon;
        private Button button;

        private void Awake()
        {
            EnsureButton();
            Disable();
        }

        Button EnsureButton()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            return button;
        }

        public void EnableFor(UnlockableSO item, UnityAction callback)
        {
            Button btn = EnsureButton();
            if (btn == null || icon == null)
            {
                return;
            }

            btn.onClick.RemoveAllListeners();
            btn.interactable = true;
            btn.onClick.AddListener(callback);
            icon.gameObject.SetActive(true);
            icon.sprite = item.Icon;
        }

        public void Disable()
        {
            Button btn = EnsureButton();
            if (btn != null)
            {
                btn.interactable = false;
                btn.onClick.RemoveAllListeners();
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }
        }
    }
}