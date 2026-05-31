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

        void SyncSliderFromController()
        {
            AudioVolumeController controller = AudioVolumeController.Instance;
            if (controller == null)
            {
                return;
            }

            slider.SetValueWithoutNotify(controller.GetVolume(channel));
        }

        void OnSliderChanged(float value)
        {
            AudioVolumeController controller = AudioVolumeController.Instance;
            if (controller == null)
            {
                return;
            }

            controller.SetVolume(channel, value);
        }
    }
}
