using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Chat lobby giống sample Mirror Chat — gửi tin qua server rồi broadcast Rpc.
    /// Cách hoạt động: Command không authority với sender; server map connection → tên rồi RpcReceive cho mọi client.
    /// </summary>
    public class RtsLobbyChat : NetworkBehaviour
    {
        public static RtsLobbyChat Instance { get; private set; }

        static readonly Dictionary<NetworkConnectionToClient, string> ConnNames =
            new Dictionary<NetworkConnectionToClient, string>();

        [SerializeField] Text chatHistory;
        [SerializeField] Scrollbar scrollbar;
        [SerializeField] InputField chatMessage;
        [SerializeField] Button sendButton;

        public static string LocalPlayerName { get; private set; }

        void Awake()
        {
            WireChatUi();
        }

        /// <summary>
        /// Mục tiêu: Gắn Gửi / onEndEdit / onValueChanged cho chat vì scene có thể không lưu sự kiện Button/InputField.
        /// Cách hoạt động: RemoveAllListeners rồi đăng ký lại các handler public của RtsLobbyChat.
        /// </summary>
        void WireChatUi()
        {
            if (sendButton != null)
            {
                sendButton.onClick.RemoveAllListeners();
                sendButton.onClick.AddListener(UiSendMessage);
            }

            if (chatMessage != null)
            {
                chatMessage.onValueChanged.RemoveAllListeners();
                chatMessage.onValueChanged.AddListener(UiOnMessageChanged);
                chatMessage.onEndEdit.RemoveAllListeners();
                chatMessage.onEndEdit.AddListener(UiOnEndEdit);
            }
        }

        public override void OnStartServer()
        {
            Instance = this;
            ConnNames.Clear();
        }

        public override void OnStartClient()
        {
            Instance = this;
            if (chatHistory != null)
                chatHistory.text = "";
        }

        public static void ClearConnectionName(NetworkConnectionToClient conn)
        {
            if (conn != null)
                ConnNames.Remove(conn);
        }

        [ClientRpc]
        void RpcReceive(string playerName, string message)
        {
            if (chatHistory == null)
                return;
            bool self = playerName == LocalPlayerName;
            string line = self
                ? $"<color=#ff6666>{playerName}:</color> {message}\n"
                : $"<color=#6699ff>{playerName}:</color> {message}\n";
            chatHistory.text += line;
            if (scrollbar != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollbar.value = 0;
            }
        }

        [Command(requiresAuthority = false)]
        void CmdSend(string message, NetworkConnectionToClient sender = null)
        {
            if (sender == null || string.IsNullOrWhiteSpace(message))
                return;

            if (!ConnNames.TryGetValue(sender, out _))
            {
                var id = sender.identity;
                if (id != null)
                {
                    var p = id.GetComponent<RtsLobbyPlayer>();
                    if (p != null)
                        ConnNames[sender] = p.DisplayName;
                }
            }

            if (!ConnNames.TryGetValue(sender, out string name))
                name = "Unknown";

            RpcReceive(name, message.Trim());
        }

        public void UiSendMessage()
        {
            if (chatMessage == null || string.IsNullOrWhiteSpace(chatMessage.text))
                return;
            CmdSend(chatMessage.text.Trim());
            chatMessage.text = "";
            chatMessage.ActivateInputField();
        }

        public void UiOnMessageChanged(string text)
        {
            if (sendButton != null)
                sendButton.interactable = !string.IsNullOrWhiteSpace(text);
        }

        public void UiOnEndEdit(string text)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                UiSendMessage();
        }

        public static void SetLocalNameForChat(string name)
        {
            LocalPlayerName = name;
        }

#if UNITY_EDITOR
        /// <summary>Chỉ gọi từ Editor khi sinh scene — gán tham chiếu UI an toàn hơn SerializedObject.</summary>
        public void EditorAssignUi(Text history, Scrollbar scroll, InputField message, Button send)
        {
            chatHistory = history;
            scrollbar = scroll;
            chatMessage = message;
            sendButton = send;
        }
#endif
    }
}
