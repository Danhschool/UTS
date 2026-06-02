#if UNITY_EDITOR
using GameDevTV.RTS.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Audio.Editor
{
    /// <summary>
    /// Gắn AudioSettingsVolumePanelBinder + AudioVolumeSliderBinder cho Dialog Setting MainMenu.
    /// </summary>
    public static class MainMenuAudioSettingsSetupEditor
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("ProjectRTS/Audio/Wire MainMenu Volume Sliders")]
        public static void WireMainMenuVolumeSliders()
        {
            AudioMixerVolumeExposeEditor.ExposeVolumeParameters(logSuccess: false);

            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("MainMenu.unity"))
            {
                EditorSceneManager.OpenScene(MainMenuScenePath);
            }

            Transform dialogSetting = FindDeepChildInScene("Dialog Setting");
            if (dialogSetting == null)
            {
                Debug.LogError("[MainMenuAudioSettings] Không tìm thấy Dialog Setting.");
                return;
            }

            AudioSettingsVolumePanelBinder panelBinder = dialogSetting.GetComponent<AudioSettingsVolumePanelBinder>();
            if (panelBinder == null)
            {
                panelBinder = dialogSetting.gameObject.AddComponent<AudioSettingsVolumePanelBinder>();
            }

            SerializedObject serialized = new SerializedObject(panelBinder);
            serialized.FindProperty("searchRoot").objectReferenceValue = dialogSetting;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            panelBinder.BindAllVolumeSliders();
            EditorUtility.SetDirty(panelBinder);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log(
                "[MainMenuAudioSettings] Đã gắn volume sliders (Master, Music, Sfx, UI). "
                + "Play MainMenu → Setting để test kéo slider.");
        }

        static Transform FindDeepChildInScene(string objectName)
        {
            Transform[] roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != objectName)
                {
                    continue;
                }

                if (roots[i].parent != null)
                {
                    return roots[i];
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindDeepChild(roots[i], objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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
#endif
