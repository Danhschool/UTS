using GameDevTV.RTS.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Một nút map trong scrollview — nhãn, trạng thái chọn (Img Select / Unselect).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PregameMapSelectItemView : MonoBehaviour
    {
        const string SelectImageName = "Img Select";
        const string UnselectImageName = "Img Unselect";

        [SerializeField] Button button;
        [SerializeField] TMP_Text label;
        [SerializeField] Image buttonImage;
        [SerializeField] Color enabledColor = Color.white;
        [SerializeField] Color disabledColor = Color.black;

        UiExclusiveSelectOption _selectOption;
        UiExclusiveSelectGroup _group;
        int _index = -1;
        UnityEngine.Events.UnityAction _clickHandler;

        /// <summary>
        /// Mục tiêu: Áp dữ liệu map lên nút vừa spawn và nối click → chọn map trong group.
        /// Cách hoạt động: Gán label/tên; cấu hình UiExclusiveSelectOption; đăng ký Button.onClick gọi group.Select.
        /// </summary>
        public void Bind(PregameMapEntry entry, UiExclusiveSelectGroup group, int index)
        {
            _group = group;
            _index = index;
            ResolveReferences();

            string displayName = string.IsNullOrWhiteSpace(entry.displayName)
                ? entry.gameplaySceneName
                : entry.displayName.Trim();

            gameObject.name = $"MapButton_{displayName}";

            if (label != null)
            {
                label.text = displayName;
                label.raycastTarget = false;
            }

            if (button != null)
            {
                button.interactable = true;
                if (_clickHandler != null)
                {
                    button.onClick.RemoveListener(_clickHandler);
                }

                _clickHandler = OnButtonClicked;
                button.onClick.AddListener(_clickHandler);
            }

            DisableHoverRevealPanel();
            ConfigureSelectOption();
            SetInteractionEnabled(true);
        }

        /// <summary>
        /// Mục tiêu: Client không chọn map — hiển thị đen giống nút Lưu settings khi disabled.
        /// Cách hoạt động: Tắt Button.interactable và đổi màu Image nền nút.
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            if (button != null)
            {
                button.interactable = enabled;
            }

            if (buttonImage != null)
            {
                buttonImage.color = enabled ? enabledColor : disabledColor;
            }
        }

        void OnButtonClicked()
        {
            if (_group != null && _index >= 0)
            {
                _group.Select(_index, notify: true);
            }
        }

        void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }

            if (buttonImage == null && button != null)
            {
                buttonImage = button.GetComponent<Image>();
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh HoverRevealPanel trên nút map chặn/đè hành vi chọn exclusive.
        /// Cách hoạt động: Tắt component HoverRevealPanel nếu có trên prefab nút map.
        /// </summary>
        void DisableHoverRevealPanel()
        {
            HoverRevealPanel[] hoverPanels = GetComponentsInChildren<HoverRevealPanel>(true);
            for (int i = 0; i < hoverPanels.Length; i++)
            {
                hoverPanels[i].enabled = false;
            }
        }

        void ConfigureSelectOption()
        {
            Transform buttonRoot = button != null ? button.transform : transform;

            _selectOption = buttonRoot.GetComponent<UiExclusiveSelectOption>();
            if (_selectOption == null)
            {
                _selectOption = buttonRoot.gameObject.AddComponent<UiExclusiveSelectOption>();
            }

            GameObject selectVisual = FindChildByName(transform, SelectImageName);
            GameObject unselectVisual = FindChildByName(transform, UnselectImageName);

            _selectOption.Configure(button, selectVisual, unselectVisual);
        }

        static GameObject FindChildByName(Transform parent, string childName)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    return children[i].gameObject;
                }
            }

            return null;
        }
    }
}
