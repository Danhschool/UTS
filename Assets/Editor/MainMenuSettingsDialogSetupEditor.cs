#if UNITY_EDITOR
using GameDevTV.RTS.UI.Pregame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Gắn logic Settings dialog (tab Audio/Hotkey, staging Save/Reset) cho MainMenu.
    /// </summary>
    public static class MainMenuSettingsDialogSetupEditor
    {
        const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("ProjectRTS/UI/Setup MainMenu Settings Dialog Logic")]
        public static void SetupMainMenuSettingsDialogLogic()
        {
            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("MainMenu.unity"))
            {
                EditorSceneManager.OpenScene(MainMenuScenePath);
            }

            Transform dialog = FindDeepChildInScene("Dialog Setting");
            if (dialog == null)
            {
                dialog = FindDeepChildInScene("Panel Setting");
            }

            if (dialog == null)
            {
                Debug.LogError("[MainMenuSettingsDialogSetup] Không tìm thấy Dialog/Panel Setting.");
                return;
            }

            MainMenuSettingsDialogController controller = dialog.GetComponent<MainMenuSettingsDialogController>();
            if (controller == null)
            {
                controller = dialog.gameObject.AddComponent<MainMenuSettingsDialogController>();
            }

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("searchRoot").objectReferenceValue = dialog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MainMenuSettingsDialogSetup] Đã gắn MainMenuSettingsDialogController.");
        }

        static Transform FindDeepChildInScene(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    return transforms[i];
                }
            }

            return null;
        }
    }
}
#endif
