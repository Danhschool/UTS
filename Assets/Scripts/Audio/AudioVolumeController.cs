using UnityEngine;
using UnityEngine.Audio;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Lưu/load volume (PlayerPrefs), áp dụng lên AudioMixer và/hoặc AudioPlaybackService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioVolumeController : MonoBehaviour
    {
        const string PrefPrefix = "RTS.Audio.Volume.";

        public static AudioVolumeController Instance { get; private set; }

        [SerializeField] AudioMixer audioMixer;
        [SerializeField] bool loadSavedVolumesOnAwake = true;
        [SerializeField] bool useMixerWhenAssigned = true;
        [Header("Defaults (0–1)")]
        [SerializeField] float defaultMaster = 1f;
        [SerializeField] float defaultMusic = 0.7f;
        [SerializeField] float defaultSfx = 0.85f;
        [SerializeField] float defaultUi = 0.75f;
        [SerializeField] float defaultVoice = 0.85f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (loadSavedVolumesOnAwake)
            {
                ApplyAllFromStorage();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Mục tiêu: Slider Settings đổi volume runtime.
        /// Cách hoạt động: Clamp 0–1, lưu PlayerPrefs, gửi tới mixer (dB) và playback service.
        /// </summary>
        public void SetVolume(AudioVolumeChannel channel, float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            PlayerPrefs.SetFloat(GetPrefKey(channel), linear01);
            PlayerPrefs.Save();
            ApplyVolume(channel, linear01);
        }

        public float GetVolume(AudioVolumeChannel channel)
        {
            if (PlayerPrefs.HasKey(GetPrefKey(channel)))
            {
                return PlayerPrefs.GetFloat(GetPrefKey(channel));
            }

            return GetDefaultVolume(channel);
        }

        public void ApplyAllFromStorage()
        {
            ApplyVolume(AudioVolumeChannel.Master, GetVolume(AudioVolumeChannel.Master));
            ApplyVolume(AudioVolumeChannel.Music, GetVolume(AudioVolumeChannel.Music));
            ApplyVolume(AudioVolumeChannel.Sfx, GetVolume(AudioVolumeChannel.Sfx));
            ApplyVolume(AudioVolumeChannel.Ui, GetVolume(AudioVolumeChannel.Ui));
            ApplyVolume(AudioVolumeChannel.Voice, GetVolume(AudioVolumeChannel.Voice));
        }

        void ApplyVolume(AudioVolumeChannel channel, float linear01)
        {
            bool hasMixer = useMixerWhenAssigned && audioMixer != null;
            if (hasMixer)
            {
                string param = GetMixerParameterName(channel);
                if (!string.IsNullOrEmpty(param))
                {
                    float db = LinearToDecibels(linear01);
                    if (audioMixer.SetFloat(param, db))
                    {
                        return;
                    }
                }
            }

            AudioAccess.Service?.SetVolume(channel, linear01);
        }

        static string GetMixerParameterName(AudioVolumeChannel channel)
        {
            return channel switch
            {
                AudioVolumeChannel.Master => AudioMixerParameterNames.MasterVolume,
                AudioVolumeChannel.Music => AudioMixerParameterNames.MusicVolume,
                AudioVolumeChannel.Sfx => AudioMixerParameterNames.SfxVolume,
                AudioVolumeChannel.Ui => AudioMixerParameterNames.UiVolume,
                AudioVolumeChannel.Voice => AudioMixerParameterNames.VoiceVolume,
                _ => null
            };
        }

        static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f)
            {
                return -80f;
            }

            return Mathf.Log10(linear) * 20f;
        }

        string GetPrefKey(AudioVolumeChannel channel) => PrefPrefix + channel;

        float GetDefaultVolume(AudioVolumeChannel channel)
        {
            return channel switch
            {
                AudioVolumeChannel.Master => defaultMaster,
                AudioVolumeChannel.Music => defaultMusic,
                AudioVolumeChannel.Sfx => defaultSfx,
                AudioVolumeChannel.Ui => defaultUi,
                AudioVolumeChannel.Voice => defaultVoice,
                _ => 1f
            };
        }
    }
}
