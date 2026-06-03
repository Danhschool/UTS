#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Audio;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Pregame;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Gắn PregameSetupUIController + MenuAudioController cho SSScene và thêm scene vào Build Settings.
    /// </summary>
    public static class PregameSetupSceneSetupEditor
    {
        const string SetupScenePath = "Assets/Scenes/SSScene.unity";

        [MenuItem("ProjectRTS/Pregame/Setup SSScene")]
        public static void SetupSSScene()
        {
            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("SSScene.unity"))
            {
                EditorSceneManager.OpenScene(SetupScenePath);
            }

            EnsureSceneInBuildSettings(SetupScenePath, insertAfter: "Assets/Scenes/MainMenu.unity");

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[PregameSetupSceneSetup] SSScene không có Canvas.");
                return;
            }

            EnsurePregameSetupController(canvas);
            EnsureSetupSceneAudio(canvas);
            ExitConfirmDialogSetupEditor.AddExitConfirmDialogToActiveScene();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[PregameSetupSceneSetup] Đã gắn PregameSetupUIController + audio SSScene + Build Settings.");
        }

        static void EnsurePregameSetupController(Canvas canvas)
        {
            PregameSetupUIController controller = canvas.GetComponent<PregameSetupUIController>();
            if (controller == null)
            {
                controller = canvas.gameObject.AddComponent<PregameSetupUIController>();
            }

            Transform root = canvas.transform;
            GameObject aiPanel = FindDeepChild(root, "AI Panel")?.gameObject;
            GameObject mpPanel = FindDeepChild(root, "MP Panel")?.gameObject;
            Button buttonStart = FindDeepChild(root, "Button Start")?.GetComponent<Button>();
            Button buttonBack = FindDeepChild(root, "Button Back")?.GetComponent<Button>();
            Button buttonExit = FindDeepChild(root, "Button Exit")?.GetComponent<Button>();
            Button buttonCreateRoom = FindDeepChild(root, "Button Start (1)")?.GetComponent<Button>();

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("searchRoot").objectReferenceValue = root;
            serialized.FindProperty("singlePlayerAiPanel").objectReferenceValue = aiPanel;
            serialized.FindProperty("multiplayerPanel").objectReferenceValue = mpPanel;

            UiExclusiveSelectGroup difficultyGroup = aiPanel != null
                ? aiPanel.GetComponent<UiExclusiveSelectGroup>()
                : null;
            if (difficultyGroup == null && aiPanel != null)
            {
                difficultyGroup = aiPanel.AddComponent<UiExclusiveSelectGroup>();
            }

            serialized.FindProperty("difficultyOptionsRoot").objectReferenceValue = aiPanel != null ? aiPanel.transform : null;
            serialized.FindProperty("difficultySelectGroup").objectReferenceValue = difficultyGroup;
            serialized.FindProperty("buttonStart").objectReferenceValue = buttonStart;
            serialized.FindProperty("buttonBack").objectReferenceValue = buttonBack;
            serialized.FindProperty("buttonExit").objectReferenceValue = buttonExit;
            serialized.FindProperty("buttonCreateRoom").objectReferenceValue = buttonCreateRoom;
            serialized.FindProperty("createExitButtonIfMissing").boolValue = buttonExit == null;

            RtsLobbyUI lobbyUi = mpPanel != null ? mpPanel.GetComponent<RtsLobbyUI>() : null;
            if (lobbyUi == null && mpPanel != null)
            {
                lobbyUi = mpPanel.GetComponentInChildren<RtsLobbyUI>(true);
            }

            serialized.FindProperty("lobbyUi").objectReferenceValue = lobbyUi;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        static void EnsureSetupSceneAudio(Canvas canvas)
        {
            MenuAudioController menuAudio = canvas.GetComponent<MenuAudioController>();
            if (menuAudio == null)
            {
                menuAudio = canvas.gameObject.AddComponent<MenuAudioController>();
            }

            SerializedObject serialized = new SerializedObject(menuAudio);
            serialized.FindProperty("startMenuMusic").boolValue = false;
            serialized.FindProperty("buttonClickCue").enumValueIndex = (int)AudioCueId.UiSelect;
            serialized.FindProperty("includeInactiveButtons").boolValue = true;
            serialized.FindProperty("buttonSearchRoot").objectReferenceValue = canvas.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menuAudio);
        }

        static void EnsureSceneInBuildSettings(string scenePath, string insertAfter)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path.Replace('\\', '/') == scenePath))
            {
                return;
            }

            int insertIndex = 0;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path.Replace('\\', '/') == insertAfter)
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            scenes.Insert(insertIndex, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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
