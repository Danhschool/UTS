using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    public interface IAudioPlaybackService
    {
        void Play2D(AudioCueId cue);
        void Play3D(AudioCueId cue, Vector3 worldPosition);
        void StartMusic();
        void StartMenuMusic();
        void StopMusic();
        void SetMusicEnabled(bool enabled);
        void SetVolume(AudioVolumeChannel channel, float linear01);
        float GetVolume(AudioVolumeChannel channel);
    }
}
