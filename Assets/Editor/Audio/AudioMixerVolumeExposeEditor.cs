#if UNITY_EDITOR
using System.Collections.Generic;
using GameDevTV.RTS.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace GameDevTV.RTS.Audio.Editor
{
    /// <summary>
    /// SRP: Expose volume parameters trên RTS Audio Mixer để AudioVolumeController.SetFloat không cảnh báo.
    /// Chỉ dùng SerializedObject — không phụ thuộc UnityEditor.Audio internal API.
    /// </summary>
    public static class AudioMixerVolumeExposeEditor
    {
        const string MixerPath = "Assets/Audio/NewAudioMixer.mixer";

        static readonly (string GroupName, string ExposedName)[] VolumeBindings =
        {
            ("Master", AudioMixerParameterNames.MasterVolume),
            ("MusicVolume", AudioMixerParameterNames.MusicVolume),
            ("SfxVolume", AudioMixerParameterNames.SfxVolume),
            ("UiVolume", AudioMixerParameterNames.UiVolume),
            ("VoiceVolume", AudioMixerParameterNames.VoiceVolume),
        };

        [InitializeOnLoadMethod]
        static void ScheduleAutoFix()
        {
            EditorApplication.delayCall += TryAutoExpose;
        }

        [MenuItem("ProjectRTS/Audio/Expose Mixer Volume Parameters")]
        [MenuItem("RTS/Audio/Expose Mixer Volume Parameters")]
        public static void ExposeFromMenu()
        {
            ExposeVolumeParameters(logSuccess: true);
        }

        static void TryAutoExpose()
        {
            if (!NeedsExpose())
            {
                return;
            }

            ExposeVolumeParameters(logSuccess: false);
        }

        static bool NeedsExpose()
        {
            AudioMixer mixer = LoadMixer();
            if (mixer == null)
            {
                return false;
            }

            HashSet<string> required = new(VolumeBindings.Length);
            for (int i = 0; i < VolumeBindings.Length; i++)
            {
                required.Add(VolumeBindings[i].ExposedName);
            }

            SerializedObject serialized = new SerializedObject(mixer);
            SerializedProperty exposed = serialized.FindProperty("m_ExposedParameters");
            if (exposed == null)
            {
                return true;
            }

            for (int i = 0; i < exposed.arraySize; i++)
            {
                string name = exposed.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                required.Remove(name);
            }

            return required.Count > 0;
        }

        /// <summary>
        /// Mục tiêu: Expose Volume của từng mixer group với tên khớp AudioMixerParameterNames.
        /// Cách hoạt động: Đọc m_Volume GUID từ subasset group, ghi vào m_ExposedParameters của mixer.
        /// </summary>
        public static void ExposeVolumeParameters(bool logSuccess)
        {
            AudioMixer mixer = LoadMixer();
            if (mixer == null)
            {
                Debug.LogWarning($"[RTS Audio] Không tìm thấy mixer tại {MixerPath}");
                return;
            }

            Dictionary<string, string> volumeGuidsByGroup = CollectGroupVolumeGuids(MixerPath);
            SerializedObject serialized = new SerializedObject(mixer);
            SerializedProperty exposed = serialized.FindProperty("m_ExposedParameters");
            if (exposed == null)
            {
                Debug.LogError("[RTS Audio] Mixer không có property m_ExposedParameters.");
                return;
            }

            bool changed = false;
            for (int i = 0; i < VolumeBindings.Length; i++)
            {
                (string groupName, string exposedName) = VolumeBindings[i];
                if (!volumeGuidsByGroup.TryGetValue(groupName, out string volumeGuidHex))
                {
                    Debug.LogWarning($"[RTS Audio] Mixer thiếu group '{groupName}'.");
                    continue;
                }

                if (TryUpdateExposedEntry(exposed, volumeGuidHex, exposedName))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();

            if (logSuccess)
            {
                Debug.Log("[RTS Audio] Đã expose volume parameters trên NewAudioMixer.");
            }
        }

        static AudioMixer LoadMixer()
        {
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        static Dictionary<string, string> CollectGroupVolumeGuids(string mixerPath)
        {
            Dictionary<string, string> result = new(VolumeBindings.Length);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(mixerPath);
            for (int i = 0; i < assets.Length; i++)
            {
                Object asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty nameProp = serialized.FindProperty("m_Name");
                SerializedProperty volumeProp = serialized.FindProperty("m_Volume");
                if (nameProp == null || volumeProp == null)
                {
                    continue;
                }

                string groupName = nameProp.stringValue;
                if (string.IsNullOrEmpty(groupName))
                {
                    continue;
                }

                string volumeGuidHex = ReadGuidHex(volumeProp);
                if (string.IsNullOrEmpty(volumeGuidHex))
                {
                    continue;
                }

                result[groupName] = volumeGuidHex;
            }

            return result;
        }

        static bool TryUpdateExposedEntry(SerializedProperty exposedArray, string volumeGuidHex, string exposedName)
        {
            volumeGuidHex = NormalizeGuidHex(volumeGuidHex);

            for (int i = 0; i < exposedArray.arraySize; i++)
            {
                SerializedProperty element = exposedArray.GetArrayElementAtIndex(i);
                if (!GuidElementMatches(element.FindPropertyRelative("guid"), volumeGuidHex))
                {
                    continue;
                }

                SerializedProperty nameProp = element.FindPropertyRelative("name");
                if (nameProp.stringValue == exposedName)
                {
                    return false;
                }

                nameProp.stringValue = exposedName;
                return true;
            }

            int index = exposedArray.arraySize;
            exposedArray.InsertArrayElementAtIndex(index);
            SerializedProperty newElement = exposedArray.GetArrayElementAtIndex(index);
            WriteGuidHex(newElement.FindPropertyRelative("guid"), volumeGuidHex);
            newElement.FindPropertyRelative("name").stringValue = exposedName;
            return true;
        }

        static string ReadGuidHex(SerializedProperty guidProp)
        {
            if (guidProp == null)
            {
                return null;
            }

            if (guidProp.propertyType == SerializedPropertyType.String)
            {
                return NormalizeGuidHex(guidProp.stringValue);
            }

            SerializedProperty data = guidProp.FindPropertyRelative("data");
            if (data != null && data.propertyType == SerializedPropertyType.String)
            {
                return NormalizeGuidHex(data.stringValue);
            }

            SerializedProperty g0 = guidProp.FindPropertyRelative("m_GUID_0");
            if (g0 == null)
            {
                return null;
            }

            uint part0 = (uint)g0.longValue;
            uint part1 = (uint)guidProp.FindPropertyRelative("m_GUID_1").longValue;
            uint part2 = (uint)guidProp.FindPropertyRelative("m_GUID_2").longValue;
            uint part3 = (uint)guidProp.FindPropertyRelative("m_GUID_3").longValue;
            return $"{part0:x8}{part1:x8}{part2:x8}{part3:x8}";
        }

        static void WriteGuidHex(SerializedProperty guidProp, string volumeGuidHex)
        {
            if (guidProp == null)
            {
                return;
            }

            if (guidProp.propertyType == SerializedPropertyType.String)
            {
                guidProp.stringValue = volumeGuidHex;
                return;
            }

            SerializedProperty data = guidProp.FindPropertyRelative("data");
            if (data != null && data.propertyType == SerializedPropertyType.String)
            {
                data.stringValue = volumeGuidHex;
            }
        }

        static bool GuidElementMatches(SerializedProperty guidProp, string targetHex)
        {
            string current = ReadGuidHex(guidProp);
            return !string.IsNullOrEmpty(current)
                && NormalizeGuidHex(current) == NormalizeGuidHex(targetHex);
        }

        static string NormalizeGuidHex(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
#endif
