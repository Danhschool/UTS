#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.UI.End;
using GameDevTV.RTS.UI.InGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Tự setup scene End: Canvas, Tab UI, EndSceneOutcomeController, Build Settings.
    /// </summary>
    public static class EndSceneSetupEditor
    {
        const string EndScenePath = "Assets/Scenes/End.unity";

        [MenuItem("ProjectRTS/End/Setup End Scene (Auto)")]
        public static void SetupEndScene()
        {
            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("End.unity"))
            {
                EditorSceneManager.OpenScene(EndScenePath);
            }

            EnsureSceneInBuildSettings(EndScenePath);

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[EndSceneSetup] End.unity không có Canvas.");
                return;
            }

            FixCanvasTransform(canvas);
            FixCanvasScaler(canvas);

            Transform tabRoot = FindDeepChild(canvas.transform, "Tab");
            if (tabRoot == null)
            {
                Debug.LogError("[EndSceneSetup] Không tìm thấy Tab prefab trong Canvas.");
                return;
            }

            tabRoot.gameObject.SetActive(true);

            TMP_Text outcomeTitle = FindDeepChild(tabRoot, "Title")?.GetComponent<TMP_Text>();
            if (outcomeTitle != null)
            {
                outcomeTitle.gameObject.SetActive(true);
                outcomeTitle.text = "CHIẾN THẮNG";
                outcomeTitle.fontSize = 28f;
                outcomeTitle.fontStyle = FontStyles.Bold;
                outcomeTitle.alignment = TextAlignmentOptions.Center;
            }

            FactionSummaryScrollViewBinder scrollBinder =
                tabRoot.GetComponentInChildren<FactionSummaryScrollViewBinder>(true);
            Button closeButton = FindDeepChild(tabRoot, "Btn_Close")?.GetComponent<Button>();

            EndSceneOutcomeController controller = canvas.GetComponent<EndSceneOutcomeController>();
            if (controller == null)
            {
                controller = canvas.gameObject.AddComponent<EndSceneOutcomeController>();
            }

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("outcomeTitleText").objectReferenceValue = outcomeTitle;
            serialized.FindProperty("scrollBinder").objectReferenceValue = scrollBinder;
            serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                "[EndSceneSetup] Đã setup End.unity: Canvas scale, EndSceneOutcomeController, Build Settings. " +
                "Thắng/thua từ gameplay sẽ load scene này.",
                canvas.gameObject);
        }

        static void FixCanvasTransform(Canvas canvas)
        {
            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            if (rect.localScale == Vector3.zero)
            {
                rect.localScale = Vector3.one;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void FixCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        static void EnsureSceneInBuildSettings(string scenePath)
        {
            string normalized = scenePath.Replace('\\', '/');
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path.Replace('\\', '/').Equals(normalized)))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(normalized, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[EndSceneSetup] Đã thêm {normalized} vào Build Settings.");
        }

        static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeepChild(root.GetChild(i), name);
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
