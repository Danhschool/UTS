using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Lobby MP trên SSScene — tạo phòng Host, client join qua scrollview, Ready/Start, không chat.
    /// </summary>
    public class RtsLobbyUI : MonoBehaviour
    {
        public static RtsLobbyUI Instance { get; private set; }

        [Header("Room list")]
        [SerializeField] Transform roomListContent;
        [SerializeField] GameObject roomEntryPrefab;

        [Header("Actions")]
        [SerializeField] Button readyButton;
        [SerializeField] Button startGameButton;
        [SerializeField] GameObject readyHoverPanel;
        [SerializeField] Image readyButtonImage;
        [SerializeField] Color readyActiveColor = Color.white;
        [SerializeField] Color readyInactiveColor = Color.black;
        [SerializeField] Image startGameButtonImage;
        [SerializeField] Color startEnabledColor = Color.white;
        [SerializeField] Color startDisabledColor = Color.black;

        ColorBlock readyButtonDefaultColors;

        [Header("Network")]
        [SerializeField] RtsUniqueNameAuthenticator authenticator;
        [SerializeField] RtsLobbyRoomBroadcast roomBroadcast;

        [Header("Room")]
        [SerializeField] string defaultRoomName = "Phòng 1";

        readonly Dictionary<string, RtsLobbyRoomEntryView> roomEntries = new();
        readonly List<GameObject> runtimeEntryObjects = new();

        RtsNetworkManager Net => NetworkManager.singleton as RtsNetworkManager;
        RtsLobbyPlayer _localPlayer;
        string _localHostAddress;
        bool _isHostingRoom;

        public bool IsInNetworkSession => NetworkClient.active || NetworkServer.active;
        public bool IsHost => NetworkServer.active && NetworkClient.isConnected;

        void Awake()
        {
            ResolveRoomListContentReference();
            Instance = this;
            if (roomBroadcast == null)
            {
                roomBroadcast = GetComponent<RtsLobbyRoomBroadcast>();
            }

            if (roomBroadcast == null)
            {
                roomBroadcast = gameObject.AddComponent<RtsLobbyRoomBroadcast>();
            }

            WireActionButtons();
            CacheReadyButtonDefaults();
            ResolveReadyHoverPanelReference();
            roomBroadcast.RoomDiscovered += OnRoomDiscovered;
        }

        void ResolveReadyHoverPanelReference()
        {
            if (readyButton == null)
            {
                return;
            }

            if (readyHoverPanel != null && readyHoverPanel != readyButton.gameObject)
            {
                return;
            }

            Transform hoverChild = readyButton.transform.Find("Hover Panel");
            if (hoverChild == null)
            {
                for (int i = 0; i < readyButton.transform.childCount; i++)
                {
                    Transform child = readyButton.transform.GetChild(i);
                    if (child != null && child.name.Contains("Hover"))
                    {
                        hoverChild = child;
                        break;
                    }
                }
            }

            readyHoverPanel = hoverChild != null && hoverChild.gameObject != readyButton.gameObject
                ? hoverChild.gameObject
                : null;
        }

        void CacheReadyButtonDefaults()
        {
            if (readyButton != null)
            {
                readyButtonDefaultColors = readyButton.colors;
            }

            if (readyButtonImage == null && readyButton != null)
            {
                readyButtonImage = readyButton.GetComponent<Image>();
            }

            if (startGameButtonImage == null && startGameButton != null)
            {
                startGameButtonImage = startGameButton.GetComponent<Image>();
            }
        }

        void OnEnable()
        {
            UnityMainThreadDispatcher.EnsureInitialized();
            ResolveRoomListContentReference();
            PrepareRoomListContent();
            roomBroadcast.StartListening();
            if (_isHostingRoom || NetworkClient.active || NetworkServer.active)
            {
                SetActionButtonsVisible(true);
                UpdateReadyHoverLock();
                UpdateStartButton();
            }
            else
            {
                ResetLobbyVisualState();
            }
        }

        void OnDisable()
        {
            roomBroadcast.StopListening();
        }

        void OnDestroy()
        {
            roomBroadcast.RoomDiscovered -= OnRoomDiscovered;
            roomBroadcast.StopAdvertising();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            RefreshAllRoomEntries();
            UpdateReadyHoverLock();
            UpdateStartButton();
        }

        /// <summary>
        /// Mục tiêu: Mirror chỉ đọc NetworkManager.authenticator — đồng bộ cả reference UI (nếu có).
        /// </summary>
        void ApplyAuthenticatorPlayerName(bool asHost)
        {
            string resolved = asHost
                ? RtsLobbyPlayerNameResolver.ForHost()
                : RtsLobbyPlayerNameResolver.ForClient();

            RtsUniqueNameAuthenticator.ApplyPlayerNameForSession(asHost);

            if (authenticator != null)
            {
                authenticator.playerName = resolved;
            }
            else
            {
                authenticator = RtsUniqueNameAuthenticator.ResolveActive();
            }
        }

        void WireActionButtons()
        {
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

            SetActionButtonsVisible(false);
        }

        /// <summary>
        /// Mục tiêu: Host tạo phòng từ SSScene (nút Tạo phòng).
        /// Cách hoạt động: Gán tên+H, IP LAN tự động, StartHost và quảng bá phòng qua UDP.
        /// </summary>
        public void CreateHostRoom()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                Debug.LogWarning("[RtsLobbyUI] Đã có session mạng — bỏ qua CreateHostRoom.");
                return;
            }

            ApplyAuthenticatorPlayerName(asHost: true);

            _localHostAddress = RtsNetworkAddressUtility.GetLoopbackOrLan();
            Net?.SetNetworkAddress(_localHostAddress);
            Net?.StartHost();

            _isHostingRoom = true;
            roomBroadcast.StartAdvertising(defaultRoomName, _localHostAddress);
            UpsertLocalHostRoomEntry();
            SetActionButtonsVisible(true);
            if (readyButton == null)
            {
                Debug.LogWarning("[RtsLobbyUI] readyButton chưa gán — chạy ProjectRTS/Pregame/Wire SSScene MP Lobby UI.", this);
            }

            Debug.Log($"[RtsLobbyUI] Host tạo phòng '{defaultRoomName}' tại {_localHostAddress}.", this);
        }

        void ResolveRoomListContentReference()
        {
            if (roomListContent != null)
            {
                return;
            }

            ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null && scrollRect.content != null)
            {
                roomListContent = scrollRect.content;
            }
        }

        void OnRoomDiscovered(string roomName, string hostAddress)
        {
            if (_isHostingRoom || NetworkClient.active || NetworkServer.active)
            {
                return;
            }

            string key = BuildRoomKey(roomName, hostAddress);
            if (roomEntries.ContainsKey(key))
            {
                RefreshAllRoomEntries();
                return;
            }

            Debug.Log($"[RtsLobbyUI] Phát hiện phòng '{roomName}' tại {hostAddress}.", this);
            CreateRoomEntry(
                key,
                BuildRoomStatusText(roomName, "—", false, false, "—", false),
                () => JoinDiscoveredRoom(roomName, hostAddress),
                interactable: true);
        }

        /// <summary>
        /// Mục tiêu: Xóa button phòng tĩnh cũ trong Content (Button_Room...) trước khi spawn runtime.
        /// Cách hoạt động: Giữ template prefab (ẩn), xóa mọi child không phải RoomEntry_ runtime.
        /// </summary>
        void PrepareRoomListContent()
        {
            if (roomListContent == null)
            {
                return;
            }

            for (int i = roomListContent.childCount - 1; i >= 0; i--)
            {
                Transform child = roomListContent.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("RoomEntry_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (roomEntryPrefab != null && child.gameObject == roomEntryPrefab)
                {
                    roomEntryPrefab.SetActive(false);
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        void JoinDiscoveredRoom(string roomName, string hostAddress)
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return;
            }

            ApplyAuthenticatorPlayerName(asHost: false);

            string connectAddress = RtsNetworkAddressUtility.ResolveClientConnectAddress(hostAddress);
            Net?.SetNetworkAddress(connectAddress);
            Net?.StartClient();
            SetActionButtonsVisible(true);
            Debug.Log($"[RtsLobbyUI] Client join phòng '{roomName}' tại {connectAddress} (broadcast: {hostAddress}).", this);
        }

        void UpsertLocalHostRoomEntry()
        {
            string key = BuildRoomKey(defaultRoomName, _localHostAddress);
            CreateRoomEntry(
                key,
                BuildRoomStatusText(defaultRoomName, RtsLobbyPlayerNameResolver.ForHost(), false, false, "—", false),
                onClick: null,
                interactable: false);
        }

        void CreateRoomEntry(string key, string text, UnityEngine.Events.UnityAction onClick, bool interactable)
        {
            if (roomListContent == null)
            {
                Debug.LogWarning("[RtsLobbyUI] Thiếu roomListContent.", this);
                return;
            }

            if (roomEntries.TryGetValue(key, out RtsLobbyRoomEntryView existing))
            {
                existing.Configure(key, text, onClick, interactable);
                return;
            }

            GameObject instance = roomEntryPrefab != null
                ? Instantiate(roomEntryPrefab, roomListContent)
                : CreateFallbackRoomEntryObject();

            instance.name = $"RoomEntry_{key}";
            RtsLobbyRoomEntryView view = instance.GetComponent<RtsLobbyRoomEntryView>();
            if (view == null)
            {
                view = instance.AddComponent<RtsLobbyRoomEntryView>();
            }

            view.Configure(key, text, onClick, interactable);
            roomEntries[key] = view;
            runtimeEntryObjects.Add(instance);
        }

        GameObject CreateFallbackRoomEntryObject()
        {
            GameObject root = new GameObject("RoomEntry", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(roomListContent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 96f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.alignment = TextAnchor.UpperLeft;
            label.color = Color.white;
            label.supportRichText = false;

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);

            return root;
        }

        void RefreshAllRoomEntries()
        {
            CollectSlotStates(
                out string p1Name,
                out bool p1Ready,
                out bool p2Connected,
                out string p2Name,
                out bool p2Ready);

            foreach (KeyValuePair<string, RtsLobbyRoomEntryView> pair in roomEntries)
            {
                string roomName = ExtractRoomNameFromKey(pair.Key);
                pair.Value.SetDisplayText(
                    BuildRoomStatusText(roomName, p1Name, p1Ready, p2Connected, p2Name, p2Ready));
            }
        }

        static void CollectSlotStates(
            out string p1Name,
            out bool p1Ready,
            out bool p2Connected,
            out string p2Name,
            out bool p2Ready)
        {
            p1Name = "—";
            p2Name = "—";
            p1Ready = false;
            p2Ready = false;
            p2Connected = false;

            if (NetworkServer.active)
            {
                foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
                {
                    NetworkConnectionToClient connection = pair.Value;
                    if (connection?.identity == null)
                    {
                        continue;
                    }

                    ApplyLobbyPlayerState(connection.identity.GetComponent<RtsLobbyPlayer>(),
                        ref p1Name, ref p1Ready, ref p2Connected, ref p2Name, ref p2Ready);
                }

                return;
            }

            if (!NetworkClient.active)
            {
                return;
            }

            foreach (KeyValuePair<uint, NetworkIdentity> pair in NetworkClient.spawned)
            {
                ApplyLobbyPlayerState(pair.Value.GetComponent<RtsLobbyPlayer>(),
                    ref p1Name, ref p1Ready, ref p2Connected, ref p2Name, ref p2Ready);
            }
        }

        static void ApplyLobbyPlayerState(
            RtsLobbyPlayer player,
            ref string p1Name,
            ref bool p1Ready,
            ref bool p2Connected,
            ref string p2Name,
            ref bool p2Ready)
        {
            if (player == null)
            {
                return;
            }

            if (player.PlayerTeamIndex <= 0)
            {
                p1Name = player.DisplayName;
                p1Ready = player.IsReady;
                return;
            }

            p2Connected = true;
            p2Name = player.DisplayName;
            p2Ready = player.IsReady;
        }

        static string BuildRoomStatusText(
            string roomName,
            string p1Name,
            bool p1Ready,
            bool p2Connected,
            string p2Name,
            bool p2Ready)
        {
            string p1Status = p1Ready ? "SS" : string.Empty;
            string p2Status = p2Ready ? "SS" : string.Empty;
            string p2DisplayName = p2Connected ? p2Name : "—";
            return $"{roomName}\nP1 | {p1Name} | {p1Status}\nP2 | {p2DisplayName} | {p2Status}";
        }

        static string BuildRoomKey(string roomName, string hostAddress) =>
            $"{roomName}|{hostAddress}";

        static string ExtractRoomNameFromKey(string key)
        {
            int split = key.IndexOf('|');
            return split > 0 ? key.Substring(0, split) : key;
        }

        public static void HideLobbyCanvasForGameplay()
        {
            RtsLobbyUI[] all = Object.FindObjectsByType<RtsLobbyUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    all[i].gameObject.SetActive(false);
                }
            }
        }

        public void ShowLoginAgain()
        {
            ResetLobbyVisualState();
            ClearRoomEntries();
            _isHostingRoom = false;
            _localPlayer = null;
            roomBroadcast.StopAdvertising();
        }

        void ResetLobbyVisualState()
        {
            SetActionButtonsVisible(false);
            ApplyReadyHoverVisual(false);
        }

        void ClearRoomEntries()
        {
            roomEntries.Clear();
            for (int i = runtimeEntryObjects.Count - 1; i >= 0; i--)
            {
                if (runtimeEntryObjects[i] != null)
                {
                    Destroy(runtimeEntryObjects[i]);
                }
            }

            runtimeEntryObjects.Clear();
        }

        public void OnLocalPlayerAssigned(RtsLobbyPlayer player)
        {
            _localPlayer = player;
            SetActionButtonsVisible(true);
            if (readyButton != null)
            {
                readyButton.interactable = true;
            }

            UpdateStartButton();
            RefreshAllRoomEntries();
        }

        public void OnLobbyPlayerStateChanged()
        {
            RefreshAllRoomEntries();
            UpdateStartButton();
        }

        public void OnClickReady()
        {
            if (_localPlayer == null)
            {
                return;
            }

            bool next = !_localPlayer.IsReady;
            _localPlayer.CmdSetReady(next);
        }

        void UpdateReadyHoverLock()
        {
            if (_localPlayer == null)
            {
                ApplyReadyHoverVisual(false);
                return;
            }

            ApplyReadyHoverVisual(_localPlayer.IsReady);
        }

        /// <summary>
        /// Mục tiêu: Giữ trạng thái hover/sáng của nút Ready khi đã bật sẵn sàng.
        /// Cách hoạt động: Bật panel hover (nếu có) và đổi màu Button/Image theo trạng thái ready.
        /// </summary>
        void ApplyReadyHoverVisual(bool readyActive)
        {
            if (readyHoverPanel != null
                && readyButton != null
                && readyHoverPanel != readyButton.gameObject)
            {
                readyHoverPanel.SetActive(readyActive);
            }

            if (readyButton != null)
            {
                ColorBlock colors = readyButtonDefaultColors;
                if (readyActive)
                {
                    colors.normalColor = readyButtonDefaultColors.highlightedColor;
                    colors.selectedColor = readyButtonDefaultColors.highlightedColor;
                }

                readyButton.colors = colors;
            }

            if (readyButtonImage != null)
            {
                readyButtonImage.color = readyActive ? readyActiveColor : readyInactiveColor;
            }
        }

        public void OnClickStartGame()
        {
            if (!IsHost)
            {
                return;
            }

            if (RtsLobbyRoomMapSync.TryGetHostLobbyPlayer(out RtsLobbyPlayer hostPlayer))
            {
                hostPlayer.ApplyLobbyMapOnServer(
                    RtsLobbyRoomMapSession.PendingIndex,
                    RtsLobbyRoomMapSession.PendingSceneName);
            }

            Net?.ServerTryStartMatch();
        }

        void UpdateStartButton()
        {
            if (startGameButton == null || Net == null)
            {
                return;
            }

            bool host = IsHost;
            bool canStart = host && NetIsReadyForMatch();
            startGameButton.interactable = canStart;

            if (startGameButtonImage != null)
            {
                if (!host)
                {
                    startGameButtonImage.color = startDisabledColor;
                    return;
                }

                startGameButtonImage.color = canStart ? startEnabledColor : startDisabledColor;
            }
        }

        bool NetIsReadyForMatch()
        {
            if (!NetworkServer.active || NetworkServer.connections.Count < 2)
            {
                return false;
            }

            foreach (KeyValuePair<int, NetworkConnectionToClient> pair in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = pair.Value;
                if (connection?.identity == null)
                {
                    return false;
                }

                RtsLobbyPlayer lobbyPlayer = connection.identity.GetComponent<RtsLobbyPlayer>();
                if (lobbyPlayer == null || !lobbyPlayer.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        void SetActionButtonsVisible(bool visible)
        {
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(visible);
            }

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(visible);
            }
        }
    }
}
