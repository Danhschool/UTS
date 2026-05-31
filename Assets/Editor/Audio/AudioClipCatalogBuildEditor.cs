#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDevTV.RTS.Audio;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.Audio.Editor
{
    /// <summary>
    /// SRP: Quét Assets/Audio/ThirdParty/0AD và tạo/cập nhật AudioClipCatalogSO.
    /// </summary>
    public static class AudioClipCatalogBuildEditor
    {
        internal const string CatalogPath = "Assets/Audio/AudioClipCatalog.asset";
        internal const string AudioRoot = "Assets/Audio/ThirdParty/0AD";

        [MenuItem("RTS/Audio/Build Clip Catalog")]
        public static void BuildCatalog()
        {
            EnsureFolder("Assets/Audio");

            AudioClipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<AudioClipCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioClipCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            List<AudioClipCatalogSO.CueClipSet> sets = new(16);
            AddInterfaceClips(sets);
            AddDeathClips(sets);
            AddCombatClips(sets);
            AddGatherClips(sets);
            AddMenuMusicClips(sets);
            AddMusicClips(sets);
            AddVoiceClips(sets);

            catalog.SetCueSets(sets.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CopyCatalogToResources(catalog);
            WireBootstrapReferences(catalog);

            Debug.Log($"[RTS Audio] Built catalog with {sets.Count} cue groups at {CatalogPath}.");
        }

        static void WireBootstrapReferences(AudioClipCatalogSO catalog)
        {
            AudioBootstrap[] bootstraps = Object.FindObjectsByType<AudioBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < bootstraps.Length; i++)
            {
                SerializedObject so = new SerializedObject(bootstraps[i]);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstraps[i]);
            }
        }

        static void CopyCatalogToResources(AudioClipCatalogSO catalog)
        {
            const string resourcesDir = "Assets/Resources";
            const string resourcesCatalogPath = resourcesDir + "/AudioClipCatalog.asset";

            EnsureFolder(resourcesDir);

            AudioClipCatalogSO existing = AssetDatabase.LoadAssetAtPath<AudioClipCatalogSO>(resourcesCatalogPath);
            if (existing != null && existing != catalog)
            {
                existing.SetCueSets(catalog.Cues);
                EditorUtility.SetDirty(existing);
                return;
            }

            if (existing == catalog)
            {
                return;
            }

            if (!File.Exists(resourcesCatalogPath))
            {
                AssetDatabase.CopyAsset(CatalogPath, resourcesCatalogPath);
            }
        }

        static void AddInterfaceClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AddSingle($"{AudioRoot}/SFX/Interface/ui_select.ogg", AudioCueId.UiSelect, AudioChannel.Ui, 0.7f, sets);
            AddSingle($"{AudioRoot}/SFX/Interface/ui_command.ogg", AudioCueId.UiCommand, AudioChannel.Ui, 0.7f, sets);
            AddSingle($"{AudioRoot}/SFX/Interface/build_start.ogg", AudioCueId.BuildStart, AudioChannel.Sfx, 0.75f, sets);
            AddSingle($"{AudioRoot}/SFX/Interface/build_complete.ogg", AudioCueId.BuildComplete, AudioChannel.Sfx, 0.8f, sets);
            AddSingle($"{AudioRoot}/SFX/Interface/upgrade_complete.ogg", AudioCueId.UpgradeComplete, AudioChannel.Sfx, 0.75f, sets);
            AddSingle($"{AudioRoot}/SFX/Interface/supply_spend.ogg", AudioCueId.SupplySpend, AudioChannel.Ui, 0.5f, sets);
        }

        static void AddDeathClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AddFolder($"{AudioRoot}/SFX/Death", AudioCueId.UnitDeath, AudioChannel.Sfx, 0.85f, sets);
        }

        static void AddCombatClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AddFolderByPattern($"{AudioRoot}/SFX/Combat", AudioCueId.BuildingDeath, AudioChannel.Sfx, 0.9f, "building_death", sets);
            AddFolder($"{AudioRoot}/SFX/Combat/Bow", AudioCueId.AttackBow, AudioChannel.Sfx, 0.85f, sets);
            AddFolderByPattern($"{AudioRoot}/SFX/Combat", AudioCueId.AttackMelee, AudioChannel.Sfx, 0.85f, "swordhit", sets);
        }

        static void AddGatherClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AddFolder($"{AudioRoot}/SFX/Gather/Stone", AudioCueId.GatherStone, AudioChannel.Sfx, 0.75f, sets);
            AddFolder($"{AudioRoot}/SFX/Gather/Wood", AudioCueId.GatherWood, AudioChannel.Sfx, 0.75f, sets);
            AddFolder($"{AudioRoot}/SFX/Gather/Food", AudioCueId.GatherFood, AudioChannel.Sfx, 0.7f, sets);
        }

        static void AddFolderByPattern(
            string folder,
            AudioCueId cue,
            AudioChannel channel,
            float volume,
            string nameContains,
            List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AudioClip[] clips = LoadClipsInFolder(folder)
                .Where(clip => clip.name.Contains(nameContains))
                .ToArray();

            if (clips.Length == 0)
            {
                return;
            }

            sets.Add(new AudioClipCatalogSO.CueClipSet
            {
                Cue = cue,
                Clips = clips,
                Volume = volume,
                Channel = channel
            });
        }

        static void AddMenuMusicClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            string[] menuTracks =
            {
                $"{AudioRoot}/Music/Calm_Before_the_Storm.ogg",
                $"{AudioRoot}/Music/Mediterranean_Waves.ogg"
            };

            List<AudioClip> clips = new(2);
            for (int i = 0; i < menuTracks.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(menuTracks[i]);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                return;
            }

            sets.Add(new AudioClipCatalogSO.CueClipSet
            {
                Cue = AudioCueId.MenuMusic,
                Clips = clips.ToArray(),
                Volume = 0.55f,
                Channel = AudioChannel.Music
            });
        }

        static void AddMusicClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AddFolder($"{AudioRoot}/Music", AudioCueId.MusicTrack, AudioChannel.Music, 1f, sets);
        }

        static void AddVoiceClips(List<AudioClipCatalogSO.CueClipSet> sets)
        {
            string voiceRoot = $"{AudioRoot}/Voice/Greek";
            AddVoiceByPredicate(voiceRoot, AudioCueId.VoiceSelect, clip =>
                clip.name.Contains("what_is_it")
                || clip.name.Contains("my_lord")
                || clip.name.Contains("yes")
                || clip.name.Contains("Asyouwish")
                || clip.name.Contains("hello"),
                sets);

            AddVoiceByPredicate(voiceRoot, AudioCueId.VoiceMove, clip =>
                clip.name.Contains("_walk_") || clip.name.Contains("march"),
                sets);

            AddVoiceByPredicate(voiceRoot, AudioCueId.VoiceAttack, clip =>
                clip.name.Contains("_attack_") || clip.name.Contains("go_out_against"),
                sets);

            AddVoiceByPredicate(voiceRoot, AudioCueId.VoiceBuild, clip =>
                clip.name.Contains("_build_") || clip.name.Contains("_repair_"),
                sets);
        }

        static void AddVoiceByPredicate(
            string folder,
            AudioCueId cue,
            System.Func<AudioClip, bool> predicate,
            List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AudioClip[] clips = LoadClipsInFolder(folder)
                .Where(predicate)
                .ToArray();

            if (clips.Length == 0)
            {
                return;
            }

            sets.Add(new AudioClipCatalogSO.CueClipSet
            {
                Cue = cue,
                Clips = clips,
                Volume = 0.85f,
                Channel = AudioChannel.Voice
            });
        }

        static void AddSingle(
            string assetPath,
            AudioCueId cue,
            AudioChannel channel,
            float volume,
            List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                return;
            }

            sets.Add(new AudioClipCatalogSO.CueClipSet
            {
                Cue = cue,
                Clips = new[] { clip },
                Volume = volume,
                Channel = channel
            });
        }

        static void AddFolder(
            string folder,
            AudioCueId cue,
            AudioChannel channel,
            float volume,
            List<AudioClipCatalogSO.CueClipSet> sets)
        {
            AudioClip[] clips = LoadClipsInFolder(folder);
            if (clips.Length == 0)
            {
                return;
            }

            sets.Add(new AudioClipCatalogSO.CueClipSet
            {
                Cue = cue,
                Clips = clips,
                Volume = volume,
                Channel = channel
            });
        }

        static AudioClip[] LoadClipsInFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return System.Array.Empty<AudioClip>();
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            List<AudioClip> clips = new(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }

    [InitializeOnLoad]
    static class AudioClipCatalogAutoBootstrap
    {
        static AudioClipCatalogAutoBootstrap()
        {
            EditorApplication.delayCall += TryBuildIfMissing;
        }

        static void TryBuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(AudioClipCatalogBuildEditor.CatalogPath))
            {
                return;
            }

            if (!Directory.Exists(AudioClipCatalogBuildEditor.AudioRoot))
            {
                return;
            }

            AudioClipCatalogBuildEditor.BuildCatalog();
        }
    }
}
#endif
