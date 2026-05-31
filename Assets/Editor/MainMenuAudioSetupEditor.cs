#if UNITY_EDITOR
using GameDevTV.RTS.Audio;
using GameDevTV.RTS.Audio.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Gắn AudioSystem + MenuAudioController cho scene MainMenu.
    /// </summary>
    public static class MainMenuAudioSetupEditor
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string AudioSystemPrefabPath = "Assets/Prefab/AudioSystem.prefab";

        [MenuItem("ProjectRTS/Audio/Setup MainMenu Audio")]
        public static void SetupMainMenuAudio()
        {
            AudioClipCatalogBuildEditor.BuildCatalog();
            AudioMixerVolumeExposeEditor.ExposeVolumeParameters(logSuccess: false);

            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("MainMenu.unity"))
            {
                EditorSceneManager.OpenScene(MainMenuScenePath);
            }

            EnsureAudioSystem();
            EnsureMenuAudioController();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MainMenuAudioSetup] Đã gắn AudioSystem + MenuAudioController cho MainMenu.");
        }

        static void EnsureAudioSystem()
        {
            AudioBootstrap existing = Object.FindFirstObjectByType<AudioBootstrap>(FindObjectsInactive.Include);
            if (existing != null)
            {
                SetBootstrapStartMusicOnPlay(existing, false);
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSystemPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[MainMenuAudioSetup] Không tìm thấy {AudioSystemPrefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "AudioSystem";
            SetBootstrapStartMusicOnPlay(instance.GetComponent<AudioBootstrap>(), false);
        }

        static void SetBootstrapStartMusicOnPlay(AudioBootstrap bootstrap, bool enabled)
        {
            if (bootstrap == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("startMusicOnPlay").boolValue = enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        static void EnsureMenuAudioController()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[MainMenuAudioSetup] MainMenu không có Canvas.");
                return;
            }

            MenuAudioController controller = canvas.GetComponent<MenuAudioController>();
            if (controller == null)
            {
                controller = canvas.gameObject.AddComponent<MenuAudioController>();
            }

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("startMenuMusic").boolValue = true;
            serialized.FindProperty("buttonClickCue").enumValueIndex = (int)AudioCueId.UiSelect;
            serialized.FindProperty("includeInactiveButtons").boolValue = true;
            serialized.FindProperty("buttonSearchRoot").objectReferenceValue = canvas.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
