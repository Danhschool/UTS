using System;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    [Serializable]
    public struct AudioPlayback3DSettings
    {
        [Tooltip("0 = nghe đều như 2D; 1 = thuần 3D (dễ mất tiếng khi camera xa). RTS nên 0.1–0.3.")]
        [Range(0f, 1f)]
        public float spatialBlend;

        [Tooltip("Khoảng cách tối thiểu trước khi bắt đầu giảm volume.")]
        public float minDistance;

        [Tooltip("Tầm nghe tối đa — tăng lên nếu camera RTS cao/xa.")]
        public float maxDistance;

        public AudioRolloffMode rolloffMode;

        public static AudioPlayback3DSettings RtsDefault => new()
        {
            spatialBlend = 0.2f,
            minDistance = 8f,
            maxDistance = 180f,
            rolloffMode = AudioRolloffMode.Linear
        };
    }
}
