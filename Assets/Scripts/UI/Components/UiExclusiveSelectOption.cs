using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Một lựa chọn trong nhóm (map, độ khó…) — bật Img Select khi được chọn.
    /// Gắn lên hàng Button hoặc Easy/Medium/Hard; kéo group + Img Select vào Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiExclusiveSelectOption : MonoBehaviour
    {
        [SerializeField] UiExclusiveSelectGroup group;
        [SerializeField] Button button;
        [SerializeField] GameObject selectedVisual;

        public int Index { get; private set; } = -1;

        void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button == null)
            {
                button = GetComponentInChildren<Button>(true);
            }

            if (selectedVisual == null)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].name == "Img Select")
                    {
                        selectedVisual = children[i].gameObject;
                        break;
                    }
                }
            }

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

        /// <summary>
        /// Mục tiêu: Cập nhật viền / highlight khi option được chọn hoặc bỏ chọn.
        /// Cách hoạt động: Bật/tắt GameObject Img Select.
        /// </summary>
        internal void ApplySelectedVisual(bool selected)
        {
            if (selectedVisual != null)
            {
                selectedVisual.SetActive(selected);
            }
        }
    }
}
