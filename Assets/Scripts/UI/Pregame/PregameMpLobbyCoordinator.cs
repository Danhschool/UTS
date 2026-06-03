using System.Collections;
using GameDevTV.RTS.Game.Pregame;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Điều phối quyền lobby MP trên SSScene — client chỉ Ready/Back, host chọn map và Start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PregameMpLobbyCoordinator : MonoBehaviour
    {
        static PregameMpLobbyCoordinator _instance;

        [SerializeField] PregameMapSelectScrollBinder mapSelectBinder;
        [SerializeField] RtsLobbyUI lobbyUi;
        [SerializeField] Button buttonCreateRoom;

        bool _subscribedMapSelection;
        bool _subscribedRoomMapSync;
        bool _lastMapInteractionEnabled = true;
        Coroutine _pushHostMapRoutine;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            ResolveDependencies();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void BindDependencies(PregameMapSelectScrollBinder binder, RtsLobbyUI lobby = null)
        {
            if (binder != null)
            {
                mapSelectBinder = binder;
            }

            if (lobby != null)
            {
                lobbyUi = lobby;
            }

            ResolveDependencies();
        }

        void ResolveDependencies()
        {
            if (mapSelectBinder == null)
            {
                mapSelectBinder = GetComponent<PregameMapSelectScrollBinder>();
            }

            if (mapSelectBinder == null)
            {
                mapSelectBinder = GetComponentInChildren<PregameMapSelectScrollBinder>(true);
            }

            if (mapSelectBinder == null)
            {
                mapSelectBinder = FindFirstObjectByType<PregameMapSelectScrollBinder>(FindObjectsInactive.Include);
            }

            if (lobbyUi == null)
            {
                lobbyUi = GetComponentInChildren<RtsLobbyUI>(true);
            }

            if (buttonCreateRoom == null)
            {
                Transform found = FindDeepChild(transform, "Button Start (1)");
                if (found != null)
                {
                    buttonCreateRoom = found.GetComponent<Button>();
                }
            }
        }

        void OnEnable()
        {
            ResolveDependencies();
            SubscribeMapSelection();
            SubscribeRoomMapSync();
            ApplyLobbyRestrictions();
        }

        void OnDisable()
        {
            UnsubscribeMapSelection();
            UnsubscribeRoomMapSync();
        }

        void Update()
        {
            ApplyLobbyRestrictions();
        }

        void SubscribeMapSelection()
        {
            if (_subscribedMapSelection || mapSelectBinder == null)
            {
                return;
            }

            mapSelectBinder.SelectionChanged += OnLocalMapSelectionChanged;
            _subscribedMapSelection = true;
        }

        void UnsubscribeMapSelection()
        {
            if (!_subscribedMapSelection || mapSelectBinder == null)
            {
                return;
            }

            mapSelectBinder.SelectionChanged -= OnLocalMapSelectionChanged;
            _subscribedMapSelection = false;
        }

        void SubscribeRoomMapSync()
        {
            if (_subscribedRoomMapSync)
            {
                return;
            }

            RtsLobbyRoomMapSync.MapSelectionChanged += OnNetworkMapSelectionChanged;
            _subscribedRoomMapSync = true;
        }

        void UnsubscribeRoomMapSync()
        {
            if (!_subscribedRoomMapSync)
            {
                return;
            }

            RtsLobbyRoomMapSync.MapSelectionChanged -= OnNetworkMapSelectionChanged;
            _subscribedRoomMapSync = false;
        }

        void OnLocalMapSelectionChanged(int index)
        {
            if (PregameSessionState.PlayMode != PregamePlayMode.Multiplayer || !IsHost())
            {
                return;
            }

            string sceneName = mapSelectBinder.GetSelectedGameplayScene();
            PregameSessionState.ConfigureMultiplayer(index, sceneName);
            RtsLobbyRoomMapSession.SetPending(index, sceneName);
            CancelPushHostMapRoutine();
            PushHostMapToNetwork(index, sceneName);
        }

        void OnNetworkMapSelectionChanged(int index, string sceneName)
        {
            if (PregameSessionState.PlayMode != PregamePlayMode.Multiplayer || IsHost())
            {
                return;
            }

            if (mapSelectBinder != null
                && !mapSelectBinder.IsIndexScenePairConsistent(index, sceneName))
            {
                return;
            }

            ApplyClientMapSelection(index, sceneName);
        }

        void ApplyClientMapSelection(int index, string sceneName)
        {
            if (mapSelectBinder == null)
            {
                return;
            }

            string normalizedScene = RtsNetSceneUtility.NormalizeSceneName(sceneName);
            mapSelectBinder.ApplyNetworkSelectionByScene(normalizedScene);
            bool sceneMatchFailed = mapSelectBinder.GetSelectedGameplayScene() != normalizedScene
                && RtsNetSceneUtility.NormalizeSceneName(mapSelectBinder.GetSelectedGameplayScene()) != normalizedScene;
            if (sceneMatchFailed)
            {
                mapSelectBinder.ApplyNetworkSelection(index);
            }

            PregameSessionState.ConfigureMultiplayer(mapSelectBinder.SelectedIndex, normalizedScene);
            Debug.Log(
                $"[PregameMpLobby] Client áp map host: index={mapSelectBinder.SelectedIndex}, scene='{normalizedScene}'.",
                this);
        }

        void ApplyLobbyRestrictions()
        {
            if (PregameSessionState.PlayMode != PregamePlayMode.Multiplayer)
            {
                mapSelectBinder?.SetInteractionEnabled(true);
                SetCreateRoomAvailable(true);
                return;
            }

            bool inSession = IsInNetworkSession();
            bool host = IsHost();
            bool mapEnabled = !inSession || host;
            if (mapSelectBinder != null && mapEnabled != _lastMapInteractionEnabled)
            {
                _lastMapInteractionEnabled = mapEnabled;
            }

            mapSelectBinder?.SetInteractionEnabled(mapEnabled);
            SetCreateRoomAvailable(!inSession);
        }

        void SetCreateRoomAvailable(bool available)
        {
            if (buttonCreateRoom == null)
            {
                return;
            }

            buttonCreateRoom.interactable = available;
            buttonCreateRoom.gameObject.SetActive(available || !IsInNetworkSession());
        }

        public void NotifyHostRoomCreated()
        {
            if (mapSelectBinder == null)
            {
                return;
            }

            int index = mapSelectBinder.SelectedIndex;
            string sceneName = mapSelectBinder.GetSelectedGameplayScene();
            PregameSessionState.ConfigureMultiplayer(index, sceneName);
            RtsLobbyRoomMapSession.SetPending(index, sceneName);
            StartPushHostMapWhenReady();
        }

        /// <summary>
        /// Mục tiêu: Tránh coroutine push map cũ (Game 1) ghi đè map host vừa đổi.
        /// Cách hoạt động: Hủy coroutine trước đó trước khi chạy push mới.
        /// </summary>
        void CancelPushHostMapRoutine()
        {
            if (_pushHostMapRoutine == null)
            {
                return;
            }

            StopCoroutine(_pushHostMapRoutine);
            _pushHostMapRoutine = null;
        }

        void StartPushHostMapWhenReady()
        {
            CancelPushHostMapRoutine();
            _pushHostMapRoutine = StartCoroutine(PushHostMapWhenPlayerReady());
        }

        /// <summary>
        /// Mục tiêu: Push map host khi player spawn; luôn đọc map đang chọn trên UI (không giữ bản cũ trong closure).
        /// Cách hoạt động: Mỗi frame lấy SelectedIndex/scene từ binder rồi gọi ApplyLobbyMapOnServer.
        /// </summary>
        IEnumerator PushHostMapWhenPlayerReady()
        {
            const int maxFrames = 180;
            for (int i = 0; i < maxFrames; i++)
            {
                if (mapSelectBinder != null && PushHostMapToNetwork(
                        mapSelectBinder.SelectedIndex,
                        mapSelectBinder.GetSelectedGameplayScene()))
                {
                    _pushHostMapRoutine = null;
                    yield break;
                }

                yield return null;
            }

            _pushHostMapRoutine = null;
            Debug.LogWarning("[PregameMpLobby] Không push được map lên host player.", this);
        }

        bool PushHostMapToNetwork(int index, string sceneName)
        {
            if (!IsHost())
            {
                return false;
            }

            if (!RtsLobbyRoomMapSync.TryGetHostLobbyPlayer(out RtsLobbyPlayer hostPlayer))
            {
                return false;
            }

            hostPlayer.ApplyLobbyMapOnServer(index, sceneName);
            return true;
        }

        public static bool IsInNetworkSession() =>
            NetworkClient.active || NetworkServer.active;

        public static bool IsHost() =>
            NetworkServer.active && NetworkClient.isConnected;

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
