using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: UI offline/lobby — đặt tên, Host/Client, địa chỉ, Ready, nút bắt đầu (host).
    /// Cách hoạt động: Gán tên vào RtsUniqueNameAuthenticator trước StartHost/StartClient; Ready gửi Command qua RtsLobbyPlayer.
    /// </summary>
    public class RtsLobbyUI : MonoBehaviour
    {
        public static RtsLobbyUI Instance { get; private set; }

        [SerializeField] InputField networkAddressInput;
        [SerializeField] InputField usernameInput;
        [SerializeField] Button hostButton;
        [SerializeField] Button clientButton;
        [SerializeField] Button readyButton;
        [SerializeField] Button startGameButton;
        [SerializeField] Text statusText;
        [SerializeField] RtsUniqueNameAuthenticator authenticator;
        [SerializeField] GameObject loginPanel;
        [SerializeField] GameObject lobbyPanel;

        RtsNetworkManager Net => NetworkManager.singleton as RtsNetworkManager;
        RtsLobbyPlayer _localPlayer;

        string _cachedAddress = "localhost";

        void Awake()
        {
            Instance = this;
            if (Net != null && string.IsNullOrWhiteSpace(Net.networkAddress))
                Net.networkAddress = _cachedAddress;
            WireLobbyButtons();
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo Host/Client/Ready/Bắt đầu trận luôn có callback — file scene có thể không lưu UnityEvent PersistentCall từ lúc Generate.
        /// Cách hoạt động: Xóa listener cũ (nếu có) rồi AddListener tới các hàm public của cùng component.
        /// </summary>
        void WireLobbyButtons()
        {
            if (hostButton != null)
            {
                hostButton.onClick.RemoveAllListeners();
                hostButton.onClick.AddListener(OnClickHost);
            }

            if (clientButton != null)
            {
                clientButton.onClick.RemoveAllListeners();
                clientButton.onClick.AddListener(OnClickClient);
            }

            if (readyButton != null)
            {
                readyButton.onClick.RemoveAllListeners();
                readyButton.onClick.AddListener(OnClickReady);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveAllListeners();
                startGameButton.onClick.AddListener(OnClickStartGame);
            }
        }

        void Start()
        {
            if (networkAddressInput != null)
            {
                if (Net != null)
                    networkAddressInput.text = Net.networkAddress;
                networkAddressInput.onValueChanged.AddListener(OnAddressChanged);
            }

            if (usernameInput != null)
                usernameInput.onValueChanged.AddListener(OnUsernameChanged);

            ToggleLoginButtons();
            ShowLoginPanel(true);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnAddressChanged(string v)
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                _cachedAddress = string.Empty;
                return;
            }

            _cachedAddress = NormalizeLoopbackAddress(v);
            Net?.SetNetworkAddress(_cachedAddress);
            if (networkAddressInput != null && Net != null && networkAddressInput.text != Net.networkAddress)
                networkAddressInput.text = Net.networkAddress;
        }

        /// <summary>
        /// Mục tiêu: Chuẩn hóa ô địa chỉ để tránh lỗi gõ phổ biến (126.0.0.1 thay vì 127.0.0.1).
        /// Cách hoạt động: Trim; nếu rỗng dùng localhost; nếu đúng chuỗi nhầm thì đổi sang 127.0.0.1.
        /// </summary>
        static string NormalizeLoopbackAddress(string v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return "localhost";
            string t = v.Trim();
            if (t == "126.0.0.1")
                return "127.0.0.1";
            return t;
        }

        void OnUsernameChanged(string _)
        {
            ToggleLoginButtons();
        }

        void ToggleLoginButtons()
        {
            bool ok = usernameInput != null && !string.IsNullOrWhiteSpace(usernameInput.text);
            if (hostButton != null)
                hostButton.interactable = ok;
            if (clientButton != null)
                clientButton.interactable = ok;
        }

        public void OnClickHost()
        {
            if (authenticator != null && usernameInput != null)
                authenticator.playerName = usernameInput.text.Trim();
            RefreshAddressFromUi();
            if (string.IsNullOrWhiteSpace(_cachedAddress))
                _cachedAddress = "localhost";
            Net?.SetNetworkAddress(_cachedAddress);
            Net?.StartHost();
            OnConnectedUi();
        }

        public void OnClickClient()
        {
            if (authenticator != null && usernameInput != null)
                authenticator.playerName = usernameInput.text.Trim();
            RefreshAddressFromUi();
            if (string.IsNullOrWhiteSpace(_cachedAddress))
                ApplyDefaultClientAddress();
            Net?.SetNetworkAddress(_cachedAddress);
            Net?.StartClient();
            OnConnectedUi();
        }

        /// <summary>
        /// Mục tiêu: Tự điền IP mặc định khi bấm Client mà ô địa chỉ đang trống.
        /// Cách hoạt động: Dùng loopback 127.0.0.1 cho test local và đồng bộ lại InputField/NetworkManager.
        /// </summary>
        void ApplyDefaultClientAddress()
        {
            _cachedAddress = "127.0.0.1";
            if (networkAddressInput != null)
                networkAddressInput.text = _cachedAddress;
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ địa chỉ từ ô nhập ngay trước khi Host/Client để bắt lỗi gõ 126.0.0.1.
        /// Cách hoạt động: Chuẩn hóa chuỗi, cập nhật NetworkManager và sửa text ô nhập nếu đã sửa loopback.
        /// </summary>
        void RefreshAddressFromUi()
        {
            if (networkAddressInput == null)
                return;
            if (string.IsNullOrWhiteSpace(networkAddressInput.text))
            {
                _cachedAddress = string.Empty;
                return;
            }

            _cachedAddress = NormalizeLoopbackAddress(networkAddressInput.text);
            if (networkAddressInput.text.Trim() != _cachedAddress)
                networkAddressInput.text = _cachedAddress;
        }

        void OnConnectedUi()
        {
            ShowLoginPanel(false);
            if (statusText != null)
                statusText.text = "Lobby — chat và bấm Ready khi sẵn sàng.";
        }

        /// <summary>
        /// Mục tiêu: Ẩn UI lobby khi đã vào RtsNet_Game (tránh chữ "Lobby — chat..." che Game view).
        /// Cách hoạt động: Tắt Canvas lobby nếu component còn tồn tại sau ServerChangeScene.
        /// </summary>
        public static void HideLobbyCanvasForGameplay()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.gameObject.SetActive(false);
        }

        public void ShowLoginAgain()
        {
            ShowLoginPanel(true);
            if (usernameInput != null)
            {
                usernameInput.text = "";
                usernameInput.ActivateInputField();
            }
            ToggleLoginButtons();
            if (statusText != null)
                statusText.text = "Đã ngắt kết nối.";
        }

        void ShowLoginPanel(bool login)
        {
            if (loginPanel != null)
                loginPanel.SetActive(login);
            if (lobbyPanel != null)
                lobbyPanel.SetActive(!login);
        }

        public void OnLocalPlayerAssigned(RtsLobbyPlayer p)
        {
            _localPlayer = p;
            RtsLobbyChat.SetLocalNameForChat(p.DisplayName);
            if (readyButton != null)
                readyButton.interactable = true;
            UpdateStartButton();
        }

        public void OnClickReady()
        {
            if (_localPlayer == null)
                return;
            bool next = !_localPlayer.IsReady;
            _localPlayer.CmdSetReady(next);
            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = next ? "Hủy Ready" : "Ready";
            }
        }

        public void OnClickStartGame()
        {
            Net?.ServerTryStartMatch();
        }

        void Update()
        {
            UpdateStartButton();
        }

        void UpdateStartButton()
        {
            if (startGameButton == null || Net == null)
                return;
            bool host = NetworkServer.active && NetworkClient.isConnected;
            startGameButton.interactable = host && NetIsReadyForMatch();
        }

        bool NetIsReadyForMatch()
        {
            if (!NetworkServer.active)
                return false;
            if (NetworkServer.connections.Count < 2)
                return false;
            foreach (var kvp in NetworkServer.connections)
            {
                var c = kvp.Value;
                if (c?.identity == null)
                    return false;
                var lp = c.identity.GetComponent<RtsLobbyPlayer>();
                if (lp == null || !lp.IsReady)
                    return false;
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>Chỉ gọi từ Editor khi sinh scene.</summary>
        public void EditorAssignUi(
            InputField networkAddress,
            InputField username,
            Button host,
            Button client,
            Button ready,
            Button startGame,
            Text status,
            RtsUniqueNameAuthenticator auth,
            GameObject login,
            GameObject lobby)
        {
            networkAddressInput = networkAddress;
            usernameInput = username;
            hostButton = host;
            clientButton = client;
            readyButton = ready;
            startGameButton = startGame;
            statusText = status;
            authenticator = auth;
            loginPanel = login;
            lobbyPanel = lobby;
        }
#endif
    }
}
