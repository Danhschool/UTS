using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Phát clip 2D từ catalog, pool AudioSource, nhạc nền loop riêng.
    /// </summary>
    public sealed class AudioPlaybackService : IAudioPlaybackService
    {
        const int SfxPoolSize = 8;
        const int World3DPoolSize = 8;

        readonly AudioClipCatalogSO catalog;
        readonly Transform sourceRoot;
        readonly AudioPlayback3DSettings playback3DSettings;
        readonly AudioSource musicSource;
        readonly List<AudioSource> sfxPool = new(SfxPoolSize);
        readonly List<AudioSource> world3DPool = new(World3DPoolSize);
        readonly Dictionary<AudioChannel, AudioMixerGroup> mixerGroups;
        int nextSfxIndex;
        int nextWorld3DIndex;
        bool musicEnabled = true;
        AudioClip[] musicPlaylist = System.Array.Empty<AudioClip>();
        AudioClip[] menuMusicPlaylist = System.Array.Empty<AudioClip>();
        float masterVolume = 1f;
        float musicVolume = 1f;
        float sfxVolume = 1f;
        float uiVolume = 1f;
        float voiceVolume = 1f;
        const float BaseMusicSourceVolume = 0.45f;

        public AudioPlaybackService(
            AudioClipCatalogSO catalog,
            Transform sourceRoot,
            AudioPlayback3DSettings playback3DSettings,
            AudioMixerGroup musicMixerGroup = null,
            AudioMixerGroup sfxMixerGroup = null,
            AudioMixerGroup uiMixerGroup = null,
            AudioMixerGroup voiceMixerGroup = null)
        {
            this.catalog = catalog;
            this.sourceRoot = sourceRoot;
            this.playback3DSettings = playback3DSettings.spatialBlend <= 0f
                && playback3DSettings.maxDistance <= 0f
                    ? AudioPlayback3DSettings.RtsDefault
                    : playback3DSettings;
            mixerGroups = new Dictionary<AudioChannel, AudioMixerGroup>(4)
            {
                { AudioChannel.Music, musicMixerGroup },
                { AudioChannel.Sfx, sfxMixerGroup ?? musicMixerGroup },
                { AudioChannel.Ui, uiMixerGroup ?? sfxMixerGroup ?? musicMixerGroup },
                { AudioChannel.Voice, voiceMixerGroup ?? sfxMixerGroup ?? musicMixerGroup }
            };

            musicSource = CreateSource("MusicSource", AudioChannel.Music);
            musicSource.loop = true;
            musicSource.priority = 0;

            for (int i = 0; i < SfxPoolSize; i++)
            {
                sfxPool.Add(CreateSource($"SfxSource_{i}", AudioChannel.Sfx));
            }

            for (int i = 0; i < World3DPoolSize; i++)
            {
                world3DPool.Add(CreateWorld3DSource($"World3DSource_{i}"));
            }

            CacheMusicPlaylist();
            CacheMenuMusicPlaylist();
        }

        AudioSource CreateWorld3DSource(string name)
        {
            AudioSource source = CreateSource(name, AudioChannel.Sfx);
            ApplyWorld3DSettings(source);
            return source;
        }

        void ApplyWorld3DSettings(AudioSource source)
        {
            source.spatialBlend = playback3DSettings.spatialBlend;
            source.minDistance = playback3DSettings.minDistance;
            source.maxDistance = playback3DSettings.maxDistance;
            source.rolloffMode = playback3DSettings.rolloffMode;
            source.dopplerLevel = 0f;
            source.spread = 45f;
        }

        AudioSource CreateSource(string name, AudioChannel defaultChannel)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(sourceRoot, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = mixerGroups[defaultChannel];
            return source;
        }

        void CacheMusicPlaylist()
        {
            musicPlaylist = ResolvePlaylist(AudioCueId.MusicTrack);
        }

        void CacheMenuMusicPlaylist()
        {
            menuMusicPlaylist = ResolvePlaylist(AudioCueId.MenuMusic);
            if (menuMusicPlaylist.Length == 0)
            {
                menuMusicPlaylist = musicPlaylist;
            }
        }

        AudioClip[] ResolvePlaylist(AudioCueId cueId)
        {
            if (catalog == null || catalog.Cues == null)
            {
                return System.Array.Empty<AudioClip>();
            }

            for (int i = 0; i < catalog.Cues.Length; i++)
            {
                AudioClipCatalogSO.CueClipSet entry = catalog.Cues[i];
                if (entry.Cue != cueId || entry.Clips == null || entry.Clips.Length == 0)
                {
                    continue;
                }

                return entry.Clips;
            }

            return System.Array.Empty<AudioClip>();
        }

        /// <summary>
        /// Mục tiêu: Phát one-shot 2D theo cue (SFX, UI, voice).
        /// Cách hoạt động: Lấy clip random từ catalog, PlayOneShot trên source pool theo channel.
        /// </summary>
        public void Play2D(AudioCueId cue)
        {
            if (catalog == null || !catalog.TryGetRandomClip(cue, out AudioClip clip, out float volume, out AudioChannel channel))
            {
                return;
            }

            AudioSource source = GetNextSfxSource(channel);
            if (mixerGroups.TryGetValue(channel, out AudioMixerGroup group))
            {
                source.outputAudioMixerGroup = group;
            }
            source.PlayOneShot(clip, volume * GetChannelMultiplier(channel) * masterVolume);
        }

        /// <summary>
        /// Mục tiêu: SFX tại vị trí unit (gather, bắn cung) — nghe được trên map RTS rộng.
        /// Cách hoạt động: Pool AudioSource 3D có mixer + min/max distance cấu hình; không dùng PlayClipAtPoint.
        /// </summary>
        public void Play3D(AudioCueId cue, Vector3 worldPosition)
        {
            if (catalog == null || !catalog.TryGetRandomClip(cue, out AudioClip clip, out float volume, out AudioChannel channel))
            {
                return;
            }

            AudioSource source = GetNextWorld3DSource(channel);
            source.transform.position = worldPosition;
            ApplyWorld3DSettings(source);
            float scaledVolume = volume * GetChannelMultiplier(channel) * masterVolume;
            source.PlayOneShot(clip, scaledVolume);
        }

        /// <summary>
        /// Mục tiêu: Slider Settings chỉnh âm lượng từng nhóm.
        /// Cách hoạt động: Lưu hệ số 0–1; áp dụng khi Play2D và trên music AudioSource.
        /// </summary>
        public void SetVolume(AudioVolumeChannel channel, float linear01)
        {
            linear01 = Mathf.Clamp01(linear01);
            switch (channel)
            {
                case AudioVolumeChannel.Master:
                    masterVolume = linear01;
                    break;
                case AudioVolumeChannel.Music:
                    musicVolume = linear01;
                    break;
                case AudioVolumeChannel.Sfx:
                    sfxVolume = linear01;
                    break;
                case AudioVolumeChannel.Ui:
                    uiVolume = linear01;
                    break;
                case AudioVolumeChannel.Voice:
                    voiceVolume = linear01;
                    break;
            }

            ApplyMusicSourceVolume();
        }

        public float GetVolume(AudioVolumeChannel channel)
        {
            return channel switch
            {
                AudioVolumeChannel.Master => masterVolume,
                AudioVolumeChannel.Music => musicVolume,
                AudioVolumeChannel.Sfx => sfxVolume,
                AudioVolumeChannel.Ui => uiVolume,
                AudioVolumeChannel.Voice => voiceVolume,
                _ => 1f
            };
        }

        float GetChannelMultiplier(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Music => musicVolume,
                AudioChannel.Sfx => sfxVolume,
                AudioChannel.Ui => uiVolume,
                AudioChannel.Voice => voiceVolume,
                _ => 1f
            };
        }

        void ApplyMusicSourceVolume()
        {
            if (musicSource == null)
            {
                return;
            }

            musicSource.volume = BaseMusicSourceVolume * musicVolume * masterVolume;
        }

        AudioSource GetNextWorld3DSource(AudioChannel channel)
        {
            AudioSource source = world3DPool[nextWorld3DIndex];
            nextWorld3DIndex = (nextWorld3DIndex + 1) % world3DPool.Count;
            if (mixerGroups.TryGetValue(channel, out AudioMixerGroup group))
            {
                source.outputAudioMixerGroup = group;
            }

            return source;
        }

        AudioSource GetNextSfxSource(AudioChannel channel)
        {
            AudioSource source = sfxPool[nextSfxIndex];
            nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Count;
            return source;
        }

        /// <summary>
        /// Mục tiêu: Bật nhạc nền gameplay.
        /// Cách hoạt động: Chọn ngẫu nhiên track MusicTrack và loop.
        /// </summary>
        public void StartMusic()
        {
            StartMusicFromPlaylist(musicPlaylist);
        }

        /// <summary>
        /// Mục tiêu: Bật nhạc nền menu (calmer tracks).
        /// Cách hoạt động: Phát playlist MenuMusic; fallback MusicTrack nếu catalog chưa có.
        /// </summary>
        public void StartMenuMusic()
        {
            StartMusicFromPlaylist(menuMusicPlaylist);
        }

        void StartMusicFromPlaylist(AudioClip[] playlist)
        {
            if (!musicEnabled || musicSource == null || playlist == null || playlist.Length == 0)
            {
                return;
            }

            if (musicSource.isPlaying && musicSource.clip != null && System.Array.IndexOf(playlist, musicSource.clip) >= 0)
            {
                return;
            }

            AudioClip track = playlist[Random.Range(0, playlist.Length)];
            musicSource.clip = track;
            ApplyMusicSourceVolume();
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
            }
        }

        public void SetMusicEnabled(bool enabled)
        {
            musicEnabled = enabled;
            if (!musicEnabled)
            {
                StopMusic();
                return;
            }

            StartMusic();
        }
    }
}
