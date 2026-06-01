using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Một lựa chọn trong nhóm exclusive — Button + ảnh chọn / không chọn + text (text nằm ngoài Button cũng được).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiExclusiveSelectOption : MonoBehaviour, IPointerClickHandler
    {
        const string SelectImageName = "Img Select";
        const string UnselectImageName = "Img Unselect";

        [SerializeField] UiExclusiveSelectGroup group;
        [SerializeField] Button button;
        [SerializeField] GameObject selectedVisual;
        [SerializeField] GameObject unselectedVisual;
        [SerializeField] bool usePointerClickWhenNoButton;

        public int Index { get; private set; } = -1;

        void Awake()
        {
            ResolveButtonReference();
            ResolveVisualReferences();

            if (button != null)
            {
                button.onClick.AddListener(NotifyClicked);
            }
        }

        void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(NotifyClicked);
            }
        }

        internal void Bind(UiExclusiveSelectGroup owner, int index)
        {
            group = owner;
            Index = index;
        }

        void NotifyClicked()
        {
            group?.Select(Index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!usePointerClickWhenNoButton || button != null)
            {
                return;
            }

            group?.Select(Index);
        }

        /// <summary>
        /// Mục tiêu: Gán Button + 2 ảnh trạng thái (SSScene độ khó AI).
        /// Cách hoạt động: Lưu reference; Img Select khi chọn, Img Unselect khi không chọn.
        /// </summary>
        public void Configure(Button clickButton, GameObject selectVisual, GameObject unselectVisual)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(NotifyClicked);
            }

            button = clickButton;
            selectedVisual = selectVisual;
            unselectedVisual = unselectVisual;
            usePointerClickWhenNoButton = clickButton == null;

            if (button != null)
            {
                button.onClick.AddListener(NotifyClicked);
            }
        }

        /// <summary>
        /// Mục tiêu: Cập nhật highlight khi option được chọn hoặc bỏ chọn.
        /// Cách hoạt động: Bật Img Select + tắt Img Unselect khi chọn; ngược lại khi không chọn.
        /// </summary>
        internal void ApplySelectedVisual(bool selected)
        {
            if (selectedVisual != null)
            {
                selectedVisual.SetActive(selected);
            }

            if (unselectedVisual != null)
            {
                unselectedVisual.SetActive(!selected);
            }
        }

        void ResolveButtonReference()
        {
            if (button != null)
            {
                return;
            }

            button = GetComponent<Button>();
        }

        void ResolveVisualReferences()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (selectedVisual == null && children[i].name == SelectImageName)
                {
                    selectedVisual = children[i].gameObject;
                }

                if (unselectedVisual == null && children[i].name == UnselectImageName)
                {
                    unselectedVisual = children[i].gameObject;
                }
            }
        }
    }
}
