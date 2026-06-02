using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: View item một dòng hotkey trong scroll view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotkeySettingItemView : MonoBehaviour
    {
        public enum ItemVisualState
        {
            Normal = 0,
            Selected = 1,
            Conflict = 2
        }

        [SerializeField] TMP_Text actionDescriptionText;
        [SerializeField] TMP_Text hotkeyButtonText;
        [SerializeField] Button hotkeyButton;
        [SerializeField] Image hotkeyButtonImage;
        [SerializeField] Color normalColor = Color.clear;
        [SerializeField] Color selectedColor = Color.white;
        [SerializeField] Color conflictColor = Color.red;

        void Awake()
        {
            ResolveReferences();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị nội dung cho một dòng hotkey.
        /// Cách hoạt động: gán text mô tả bên trái và text phím trên button bên phải.
        /// </summary>
        public void SetData(string actionDescription, string hotkeyText)
        {
            ResolveReferences();

            if (actionDescriptionText != null)
            {
                actionDescriptionText.text = actionDescription;
            }

            if (hotkeyButtonText != null)
            {
                hotkeyButtonText.text = hotkeyText;
            }
        }

        /// <summary>
        /// Mục tiêu: Cho binder cập nhật chữ hiển thị phím khi người chơi rebind.
        /// Cách hoạt động: Set trực tiếp TMP text trên button phím.
        /// </summary>
        public void SetHotkeyText(string hotkeyText)
        {
            ResolveReferences();
            if (hotkeyButtonText != null)
            {
                hotkeyButtonText.text = hotkeyText;
            }
        }

        public void SetVisualState(ItemVisualState state)
        {
            ResolveReferences();
            if (hotkeyButtonImage == null)
            {
                return;
            }

            hotkeyButtonImage.color = state switch
            {
                ItemVisualState.Selected => selectedColor,
                ItemVisualState.Conflict => conflictColor,
                _ => normalColor
            };
        }

        public void RegisterClickListener(UnityEngine.Events.UnityAction action)
        {
            ResolveReferences();
            if (hotkeyButton != null)
            {
                hotkeyButton.onClick.AddListener(action);
            }
        }

        public void UnregisterClickListener(UnityEngine.Events.UnityAction action)
        {
            if (hotkeyButton != null)
            {
                hotkeyButton.onClick.RemoveListener(action);
            }
        }

        void ResolveReferences()
        {
            if (actionDescriptionText != null && hotkeyButtonText != null && hotkeyButton != null)
            {
                return;
            }

            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                {
                    continue;
                }

                bool isUnderButton = labels[i].transform.parent != null
                                     && labels[i].transform.parent.GetComponentInParent<UnityEngine.UI.Button>() != null;

                if (isUnderButton)
                {
                    hotkeyButtonText ??= labels[i];
                }
                else
                {
                    actionDescriptionText ??= labels[i];
                }
            }

            if (hotkeyButton == null)
            {
                hotkeyButton = GetComponentInChildren<Button>(true);
            }

            if (hotkeyButtonImage == null && hotkeyButton != null)
            {
                hotkeyButtonImage = hotkeyButton.GetComponent<Image>();
            }
        }
    }
}
