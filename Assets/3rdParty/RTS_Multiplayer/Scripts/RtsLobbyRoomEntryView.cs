using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Một dòng phòng trong scrollview — hiển thị trạng thái hoặc nút Join Client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsLobbyRoomEntryView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Text label;
        [SerializeField] TextMeshProUGUI tmpLabel;

        public string RoomKey { get; private set; }

        void Awake()
        {
            ResolveReferences();
        }

        void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }

            if (tmpLabel == null)
            {
                tmpLabel = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        /// <summary>
        /// Mục tiêu: Gán text phòng và callback click (Join hoặc chọn phòng).
        /// Cách hoạt động: Lưu roomKey, set label TMP hoặc UGUI Text, đăng ký onClick.
        /// </summary>
        public void Configure(string roomKey, string displayText, UnityAction onClick, bool interactable)
        {
            ResolveReferences();
            RoomKey = roomKey;
            SetDisplayText(displayText);

            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = interactable;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
        }

        public void SetDisplayText(string displayText)
        {
            if (tmpLabel != null)
            {
                tmpLabel.text = displayText;
                return;
            }

            if (label != null)
            {
                label.text = displayText;
            }
        }
    }
}
