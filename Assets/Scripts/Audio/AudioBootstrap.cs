using GameDevTV.RTS.Game.Startup;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Khởi tạo <see cref="AudioPlaybackService"/> và giữ reference DontDestroyOnLoad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioBootstrap : MonoBehaviour
    {
        const string CatalogAssetPath = "Assets/Audio/AudioClipCatalog.asset";
        const string CatalogResourcesPath = "AudioClipCatalog";

        [SerializeField] AudioClipCatalogSO catalog;
        public static AudioBootstrap Instance { get; private set; }

        [SerializeField] bool persistAcrossScenes = true;
        [SerializeField] bool startMusicOnPlay = true;
        [Header("Optional Audio Mixer groups")]
        [SerializeField] AudioMixerGroup musicGroup;
        [SerializeField] AudioMixerGroup sfxGroup;
        [SerializeField] AudioMixerGroup uiGroup;
        [SerializeField] AudioMixerGroup voiceGroup;

        [SerializeField] AudioVolumeController volumeController;

        [Header("Play3D — tầm nghe trên map RTS")]
        [SerializeField] AudioPlayback3DSettings playback3DSettings = AudioPlayback3DSettings.RtsDefault;

        Transform sourceRoot;
        AudioPlaybackService playbackService;

        /// <summary>
        /// Mục tiêu: Đảm bảo có audio system trước khi listener subscribe bus.
        /// Cách hoạt động: Trả về Instance hoặc tạo GameObject mới gắn bootstrap.
        /// </summary>
        public static AudioBootstrap EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AudioBootstrap existing = FindFirstObjectByType<AudioBootstrap>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new GameObject(nameof(AudioBootstrap));
            return go.AddComponent<AudioBootstrap>();
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
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializePlayback();
            ApplyStoredVolumes();
        }

        void ApplyStoredVolumes()
        {
            volumeController ??= GetComponent<AudioVolumeController>();
            volumeController?.ApplyAllFromStorage();
        }

        void Start()
        {
            if (!startMusicOnPlay)
            {
                return;
            }

            if (GameplayStartupScenes.IsActiveGameplayScene() && !GameplayStartupGate.IsGameplayUnlocked)
            {
                GameplayStartupGate.Unlocked += StartMusicAfterUnlock;
                return;
            }

            playbackService?.StartMusic();
        }

        void OnDestroy()
        {
            GameplayStartupGate.Unlocked -= StartMusicAfterUnlock;
            if (Instance == this)
            {
                AudioAccess.Service = null;
                Instance = null;
            }
        }

        void StartMusicAfterUnlock()
        {
            GameplayStartupGate.Unlocked -= StartMusicAfterUnlock;
            if (!startMusicOnPlay || !GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            AudioAccess.TryStartGameplayMusic();
        }

        void InitializePlayback()
        {
            ResolveCatalogReference();
            if (catalog == null)
            {
                Debug.LogWarning(
                    $"[{nameof(AudioBootstrap)}] Chưa gán AudioClipCatalogSO. Chạy menu RTS/Audio/Build Clip Catalog trong Editor.");
                return;
            }

            sourceRoot = new GameObject("AudioSources").transform;
            sourceRoot.SetParent(transform, false);

            playbackService = new AudioPlaybackService(
                catalog,
                sourceRoot,
                playback3DSettings,
                musicGroup,
                sfxGroup,
                uiGroup,
                voiceGroup);

            AudioAccess.Service = playbackService;
        }

        void ResolveCatalogReference()
        {
            if (catalog != null)
            {
                return;
            }

            catalog = Resources.Load<AudioClipCatalogSO>(CatalogResourcesPath);
#if UNITY_EDITOR
            if (catalog == null)
            {
                catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClipCatalogSO>(CatalogAssetPath);
            }
#endif
        }

#if UNITY_EDITOR
        public void SetCatalogForEditor(AudioClipCatalogSO editorCatalog)
        {
            catalog = editorCatalog;
        }
#endif
    }
}
