using GameDevTV.RTS.Game.Startup;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Static accessor cho playback service sau khi <see cref="AudioBootstrap"/> khởi tạo.
    /// </summary>
    public static class AudioAccess
    {
        public static IAudioPlaybackService Service { get; internal set; }

        public static bool IsPlaybackBlocked =>
            !GameplayStartupGate.IsGameplayUnlocked
            && GameplayStartupScenes.IsActiveGameplayScene();

        public static bool TryPlay(AudioCueId cueId)
        {
            if (Service == null || cueId == AudioCueId.None || IsPlaybackBlocked)
            {
                return false;
            }

            Service.Play2D(cueId);
            return true;
        }

        public static bool TryPlay3D(AudioCueId cueId, Vector3 worldPosition)
        {
            if (Service == null || cueId == AudioCueId.None || IsPlaybackBlocked)
            {
                return false;
            }

            Service.Play3D(cueId, worldPosition);
            return true;
        }

        /// <summary>
        /// Mục tiêu: Nhạc gameplay chỉ sau loading screen.
        /// Cách hoạt động: Bỏ qua khi session đang chặn presentation.
        /// </summary>
        public static void TryStartGameplayMusic()
        {
            if (Service == null || IsPlaybackBlocked)
            {
                return;
            }

            Service.StartMusic();
        }

        public static void TryStartMenuMusic()
        {
            if (Service == null || IsPlaybackBlocked)
            {
                return;
            }

            Service.StartMenuMusic();
        }

        public static void TryStopMusic()
        {
            Service?.StopMusic();
        }

        public static void SetVolume(AudioVolumeChannel channel, float linear01)
        {
            Service?.SetVolume(channel, linear01);
        }

        public static float GetVolume(AudioVolumeChannel channel)
        {
            return Service != null ? Service.GetVolume(channel) : 0f;
        }
    }
}
