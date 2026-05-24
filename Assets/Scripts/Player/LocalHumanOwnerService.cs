using System;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Lưu và cung cấp <see cref="ILocalHumanOwner.LocalOwner"/> cho presentation/input trên client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalHumanOwnerService : MonoBehaviour, ILocalHumanOwner
    {
        public static LocalHumanOwnerService Instance { get; private set; }

        /// <summary>MP / binder: gán lại presentation khi local owner đổi.</summary>
        public static event Action<Owner> LocalOwnerChanged;

        [SerializeField] bool persistAcrossScenes = true;

        [Header("Runtime (read-only in Play)")]
        [SerializeField] Owner inspectorLocalOwner = Owner.Invalid;
        [SerializeField] bool inspectorInitialized;

        Owner _localOwner = Owner.Invalid;
        bool _isInitialized;

        public Owner LocalOwner => _localOwner;
        public bool IsInitialized => _isInitialized;

        public bool IsLocalOwner(Owner owner) =>
            _isInitialized && owner == _localOwner;

        /// <summary>
        /// Mục tiêu: Đảm bảo có instance service trước khi lobby/offline bootstrap gán owner.
        /// Cách hoạt động: Trả về <see cref="Instance"/> hoặc tạo GameObject DontDestroyOnLoad mới gắn component.
        /// </summary>
        public static LocalHumanOwnerService EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var existing = FindFirstObjectByType<LocalHumanOwnerService>();
            if (existing != null)
                return existing;

            var go = new GameObject(nameof(LocalHumanOwnerService));
            return go.AddComponent<LocalHumanOwnerService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            TryBootstrapOfflineDefault();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Mục tiêu: Gán human owner cho máy client hiện tại (lobby MP hoặc debug).
        /// Cách hoạt động: Chỉ chấp nhận Player1/Player2; ghi đè nếu team đổi hoặc chưa init.
        /// </summary>
        public void SetLocalOwner(Owner owner)
        {
            if (!OwnerTeamMapping.IsHumanPlayer(owner))
            {
                Debug.LogError($"[LocalHumanOwnerService] Owner {owner} không phải human player.");
                return;
            }

            _localOwner = owner;
            _isInitialized = true;
            RefreshInspectorDebugFields();
            LocalOwnerChanged?.Invoke(_localOwner);
        }

        void LateUpdate()
        {
            if (Application.isPlaying)
                RefreshInspectorDebugFields();
        }

        void RefreshInspectorDebugFields()
        {
            inspectorLocalOwner = _localOwner;
            inspectorInitialized = _isInitialized;
        }

        [ContextMenu("Log Local Owner State")]
        void LogLocalOwnerState()
        {
            Debug.Log(
                $"[LocalHumanOwnerService] Initialized={_isInitialized}, LocalOwner={_localOwner}, Instance={(Instance == this ? "this" : "other")}",
                this);
        }

        void TryBootstrapOfflineDefault()
        {
            if (_isInitialized || NetworkClient.active)
                return;

            SetLocalOwner(Owner.Player1);
        }

        /// <summary>
        /// Mục tiêu: Offline / scene không Mirror — mặc định Player1 khi chưa có local player net.
        /// Cách hoạt động: Bỏ qua nếu đã init hoặc Mirror đang chạy; ngược lại gọi <see cref="SetLocalOwner"/>.
        /// </summary>
        public void TryBootstrapOfflinePlayer1()
        {
            if (_isInitialized || NetworkClient.active)
                return;

            SetLocalOwner(Owner.Player1);
        }
    }
}
