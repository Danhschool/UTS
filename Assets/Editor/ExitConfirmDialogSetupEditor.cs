#if UNITY_EDITOR
using GameDevTV.RTS.UI.Pregame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Tạo prefab Dialog Exit Confirm từ Dialog.prefab và gắn reference lên scene đang mở.
    /// </summary>
    public static class ExitConfirmDialogSetupEditor
    {
        const string DialogPrefabPath = "Assets/Prefab/UI/Dialog.prefab";
        const string ButtonPrefabPath = "Assets/Prefab/UI/Button.prefab";
        const string ExitDialogPrefabPath = "Assets/Prefab/UI/Dialog Exit Confirm.prefab";
        const string DialogObjectName = "Dialog Exit Confirm";
        const string DefaultMessage = "Bạn có chắc muốn thoát game không?";

        [MenuItem("ProjectRTS/UI/Create Exit Confirm Dialog Prefab")]
        public static void CreateExitConfirmDialogPrefab()
        {
            GameObject dialogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPrefabPath);
            if (dialogPrefab == null)
            {
                Debug.LogError($"[ExitConfirmDialogSetup] Không tìm thấy {DialogPrefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dialogPrefab);
            try
            {
                ConfigureDialogInstance(instance);
                instance.SetActive(false);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, ExitDialogPrefabPath);
                Debug.Log($"[ExitConfirmDialogSetup] Đã tạo prefab: {ExitDialogPrefabPath}", saved);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("ProjectRTS/UI/Add Exit Confirm Dialog To Active Scene")]
        public static void AddExitConfirmDialogToActiveScene()
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ExitConfirmDialogSetup] Scene không có Canvas.");
                return;
            }

            Transform existing = FindDeepChild(canvas.transform, DialogObjectName);
            GameObject dialogObject;
            ExitDialogParts parts;

            if (existing != null)
            {
                dialogObject = existing.gameObject;
                parts = ReadDialogParts(dialogObject);
            }
            else
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitDialogPrefabPath);
                if (prefab == null)
                {
                    CreateExitConfirmDialogPrefab();
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitDialogPrefabPath);
                }

                if (prefab == null)
                {
                    Debug.LogError("[ExitConfirmDialogSetup] Không tạo được prefab dialog thoát.");
                    return;
                }

                dialogObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                dialogObject.name = DialogObjectName;
                parts = ReadDialogParts(dialogObject);
                dialogObject.SetActive(false);
            }

            ExitConfirmDialog exitDialog = canvas.GetComponent<ExitConfirmDialog>();
            if (exitDialog == null)
            {
                exitDialog = canvas.gameObject.AddComponent<ExitConfirmDialog>();
            }

            WireExitConfirmDialog(exitDialog, dialogObject, parts);
            WireMenuControllers(canvas.gameObject, exitDialog);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("[ExitConfirmDialogSetup] Đã gắn Dialog Exit Confirm + ExitConfirmDialog lên Canvas.", canvas);
        }

        struct ExitDialogParts
        {
            public TMP_Text messageLabel;
            public Button confirmButton;
            public Button cancelButton;
        }

        static ExitDialogParts ConfigureDialogInstance(GameObject dialog)
        {
            dialog.name = DialogObjectName;

            TMP_Text title = FindDirectChildTmp(dialog.transform, "Text (TMP)");
            if (title != null)
            {
                title.text = "Thoát game";
            }

            Transform content = FindDeepChild(dialog.transform, "Content");
            TMP_Text message = content != null ? content.GetComponentInChildren<TMP_Text>(true) : null;
            if (message != null)
            {
                message.gameObject.name = "Txt_Message";
                message.text = DefaultMessage;
                message.alignment = TextAlignmentOptions.Center;
                message.fontSize = 32f;
            }

            Transform btnClose = FindDeepChild(dialog.transform, "Btn_Close");
            if (btnClose != null)
            {
                Object.DestroyImmediate(btnClose.gameObject);
            }

            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            Button confirm = CreateDialogButton(buttonPrefab, dialog.transform, "Btn_Confirm", "OK", new Vector2(-250f, 75f));
            Button cancel = CreateDialogButton(buttonPrefab, dialog.transform, "Btn_Cancel", "Hủy", new Vector2(-90f, 75f));

            return new ExitDialogParts
            {
                messageLabel = message,
                confirmButton = confirm,
                cancelButton = cancel
            };
        }

        static ExitDialogParts ReadDialogParts(GameObject dialog)
        {
            Transform messageTransform = FindDeepChild(dialog.transform, "Txt_Message");
            TMP_Text message = messageTransform != null
                ? messageTransform.GetComponent<TMP_Text>()
                : null;

            Button confirm = FindDeepChild(dialog.transform, "Btn_Confirm")?.GetComponent<Button>();
            Button cancel = FindDeepChild(dialog.transform, "Btn_Cancel")?.GetComponent<Button>();

            return new ExitDialogParts
            {
                messageLabel = message,
                confirmButton = confirm,
                cancelButton = cancel
            };
        }

        static Button CreateDialogButton(
            GameObject buttonPrefab,
            Transform parent,
            string objectName,
            string label,
            Vector2 anchoredPosition)
        {
            GameObject instance = buttonPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent)
                : new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));

            instance.name = objectName;
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 75f);
            rect.anchoredPosition = anchoredPosition;

            TMP_Text tmp = instance.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = label;
            }

            return instance.GetComponent<Button>();
        }

        static void WireExitConfirmDialog(ExitConfirmDialog component, GameObject dialog, ExitDialogParts parts)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty("dialogRoot").objectReferenceValue = dialog;
            serialized.FindProperty("messageLabel").objectReferenceValue = parts.messageLabel;
            serialized.FindProperty("confirmButton").objectReferenceValue = parts.confirmButton;
            serialized.FindProperty("cancelButton").objectReferenceValue = parts.cancelButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireMenuControllers(GameObject canvasObject, ExitConfirmDialog exitDialog)
        {
            MainMenuUIController mainMenu = canvasObject.GetComponent<MainMenuUIController>();
            if (mainMenu != null)
            {
                SerializedObject serialized = new SerializedObject(mainMenu);
                serialized.FindProperty("exitConfirmDialog").objectReferenceValue = exitDialog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PregameSetupUIController setup = canvasObject.GetComponent<PregameSetupUIController>();
            if (setup != null)
            {
                SerializedObject serialized = new SerializedObject(setup);
                serialized.FindProperty("exitConfirmDialog").objectReferenceValue = exitDialog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static TMP_Text FindDirectChildTmp(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name != childName)
                {
                    continue;
                }

                return child.GetComponent<TMP_Text>();
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
