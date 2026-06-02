using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Gắn Slider UI → <see cref="AudioVolumeController"/> cho một kênh volume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioVolumeSliderBinder : MonoBehaviour
    {
        [SerializeField] AudioVolumeChannel channel = AudioVolumeChannel.Master;
        [SerializeField] Slider slider;
        [SerializeField] bool applyOnStart = true;

        void Awake()
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
            }
        }

        void OnEnable()
        {
            if (slider == null)
            {
                return;
            }

            slider.onValueChanged.AddListener(OnSliderChanged);

            if (applyOnStart)
            {
                SyncSliderFromController();
            }
        }

        void OnDisable()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        /// <summary>
        /// Mục tiêu: Gán kênh + slider từ panel setup (Master/Music/…).
        /// Cách hoạt động: Lưu reference rồi đồng bộ giá trị đã lưu PlayerPrefs.
        /// </summary>
        public void Configure(AudioVolumeChannel targetChannel, Slider targetSlider = null)
        {
            channel = targetChannel;
            if (targetSlider != null)
            {
                slider = targetSlider;
            }
            else if (slider == null)
            {
                slider = GetComponent<Slider>();
            }

            EnsureSliderRange();
            SyncSliderFromController();
        }

        void EnsureSliderRange()
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }

        void SyncSliderFromController()
        {
            float volume = ResolveVolume();
            slider.SetValueWithoutNotify(volume);
        }

        void OnSliderChanged(float value)
        {
            if (AudioVolumeController.Instance != null)
            {
                AudioVolumeController.Instance.SetVolume(channel, value);
                return;
            }

            AudioAccess.SetVolume(channel, value);
        }

        float ResolveVolume()
        {
            if (AudioVolumeController.Instance != null)
            {
                return AudioVolumeController.Instance.GetVolume(channel);
            }

            return AudioAccess.GetVolume(channel);
        }
    }
}
