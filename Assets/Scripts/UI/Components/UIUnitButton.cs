using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    [RequireComponent(typeof(Button))]
    public class UIUnitButton : MonoBehaviour, IUIElement<ITransportable, UnityAction>
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

        public void EnableFor(ITransportable item, UnityAction callback)
        {
            Button btn = EnsureButton();
            if (btn == null)
            {
                return;
            }

            btn.onClick.RemoveAllListeners();
            gameObject.SetActive(true);

            if (icon != null)
            {
                icon.sprite = item.Icon;
            }

            btn.onClick.AddListener(callback);
        }

        public void Disable()
        {
            Button btn = EnsureButton();
            btn?.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }
    }
}