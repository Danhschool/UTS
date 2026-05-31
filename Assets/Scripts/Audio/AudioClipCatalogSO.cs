using System;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    [CreateAssetMenu(fileName = "AudioClipCatalog", menuName = "RTS/Audio Clip Catalog")]
    public sealed class AudioClipCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct CueClipSet
        {
            public AudioCueId Cue;
            public AudioClip[] Clips;
            [Range(0f, 1f)] public float Volume;
            public AudioChannel Channel;
        }

        [SerializeField] CueClipSet[] cues = Array.Empty<CueClipSet>();

        public CueClipSet[] Cues => cues;

        /// <summary>
        /// Mục tiêu: Lấy clip ngẫu nhiên cho một cue gameplay.
        /// Cách hoạt động: Tìm entry theo id, chọn index ngẫu nhiên trong mảng Clips.
        /// </summary>
        public bool TryGetRandomClip(
            AudioCueId cueId,
            out AudioClip clip,
            out float volume,
            out AudioChannel channel)
        {
            clip = null;
            volume = 1f;
            channel = AudioChannel.Sfx;

            if (cueId == AudioCueId.None || cues == null)
            {
                return false;
            }

            for (int i = 0; i < cues.Length; i++)
            {
                CueClipSet entry = cues[i];
                if (entry.Cue != cueId || entry.Clips == null || entry.Clips.Length == 0)
                {
                    continue;
                }

                clip = entry.Clips[UnityEngine.Random.Range(0, entry.Clips.Length)];
                volume = entry.Volume;
                channel = entry.Channel;
                return clip != null;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Editor/build pipeline gán toàn bộ cue map.
        /// Cách hoạt động: Thay mảng cues nội bộ bằng bản build mới.
        /// </summary>
        public void SetCueSets(CueClipSet[] builtCues)
        {
            cues = builtCues ?? Array.Empty<CueClipSet>();
        }
    }
}
