#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.Audio.Editor
{
    /// <summary>
    /// Hướng dẫn tạo Audio Mixer trong Editor (asset phải tạo thủ công — xem README trong menu log).
    /// </summary>
    public static class AudioMixerSetupGuide
    {
        [MenuItem("RTS/Audio/Log Mixer Setup Guide")]
        public static void LogSetupGuide()
        {
            Debug.Log(
                "[RTS Audio] Tạo Audio Mixer:\n" +
                "1. Assets → Create → Audio Mixer → đặt tại Assets/Audio/RtsAudioMixer.mixer\n" +
                "2. Nhóm: Master → Music, SFX, UI, Voice (child của Master)\n" +
                "3. Expose parameters (click Master/Music/... → Expose 'Volume'):\n" +
                $"   - Master: {AudioMixerParameterNames.MasterVolume}\n" +
                $"   - Music: {AudioMixerParameterNames.MusicVolume}\n" +
                $"   - SFX: {AudioMixerParameterNames.SfxVolume}\n" +
                $"   - UI: {AudioMixerParameterNames.UiVolume}\n" +
                $"   - Voice: {AudioMixerParameterNames.VoiceVolume}\n" +
                "4. Gán group vào AudioBootstrap (Music/SFX/UI/Voice)\n" +
                "5. Gán mixer vào AudioVolumeController trên cùng GameObject\n" +
                "6. Slider Settings: Add Component AudioVolumeSliderBinder + chọn channel");
        }
    }
}
#endif
