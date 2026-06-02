using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Gắn mọi Slider trong panel Settings theo tên hàng (Master, Music, Sfx, UI, Voice).
    /// Đặt trên root Dialog Setting hoặc Panel Setting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioSettingsVolumePanelBinder : MonoBehaviour
    {
        [SerializeField] Transform searchRoot;
        [SerializeField] string[] volumeRowNames = { "Master", "Music", "Sfx", "UI", "Voice" };
        [SerializeField] AudioVolumeChannel[] volumeChannels =
        {
            AudioVolumeChannel.Master,
            AudioVolumeChannel.Music,
            AudioVolumeChannel.Sfx,
            AudioVolumeChannel.Ui,
            AudioVolumeChannel.Voice
        };

        void Awake()
        {
            if (searchRoot == null)
            {
                searchRoot = transform;
            }
        }

        void OnEnable()
        {
            BindAllVolumeSliders();
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ slider khi mở Dialog Setting.
        /// Cách hoạt động: Ensure AudioSystem → quét hàng → Configure từng AudioVolumeSliderBinder.
        /// </summary>
        public void BindAllVolumeSliders()
        {
            AudioBootstrap.EnsureExists();
            AudioVolumeController.Instance?.ApplyAllFromStorage();

            int pairCount = Mathf.Min(volumeRowNames.Length, volumeChannels.Length);
            for (int i = 0; i < pairCount; i++)
            {
                BindRow(volumeRowNames[i], volumeChannels[i]);
            }
        }

        void BindRow(string rowName, AudioVolumeChannel channel)
        {
            if (string.IsNullOrWhiteSpace(rowName))
            {
                return;
            }

            Transform row = FindDeepChild(searchRoot, rowName);
            if (row == null)
            {
                return;
            }

            Slider slider = row.GetComponentInChildren<Slider>(true);
            if (slider == null)
            {
                Debug.LogWarning($"[AudioSettings] Hàng '{rowName}' không có Slider.", this);
                return;
            }

            AudioVolumeSliderBinder binder = slider.GetComponent<AudioVolumeSliderBinder>();
            if (binder == null)
            {
                binder = slider.gameObject.AddComponent<AudioVolumeSliderBinder>();
            }

            binder.Configure(channel, slider);
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
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
