using GameDevTV.RTS.UI.InGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    public static class MatchOutcomePanelSetupEditor
    {
        const string MenuPath = "GameDevTV/UI/InGame/Create Match Outcome Panel";

        [MenuItem(MenuPath)]
        public static void CreatePanelUnderSelection()
        {
            Transform parent = Selection.activeTransform;
            if (parent == null)
            {
                EditorUtility.DisplayDialog(
                    "Match Outcome",
                    "Chọn Runtime UI UGUI (Canvas) rồi chạy lại menu.",
                    "OK");
                return;
            }

            GameObject root = new("Panel Match Outcome", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Match Outcome Panel");
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.1f, 0.94f);

            GameObject dialog = new("Dialog", typeof(RectTransform), typeof(Image));
            dialog.transform.SetParent(root.transform, false);
            RectTransform dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(1500f, 820f);
            dialog.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.98f);

            GameObject title = CreateText(dialog.transform, "Outcome Title", "CHIẾN THẮNG", 28, FontStyles.Bold);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -16f);
            titleRect.sizeDelta = new Vector2(-32f, 48f);
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            GameObject scroll = new("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(dialog.transform, false);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(16f, 64f);
            scrollRect.offsetMax = new Vector2(-16f, -72f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            GameObject viewport = new("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scroll.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollComponent = scroll.GetComponent<ScrollRect>();
            scrollComponent.viewport = viewport.GetComponent<RectTransform>();
            scrollComponent.content = contentRect;
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;

            FactionSummaryScrollViewBinder binder = scroll.AddComponent<FactionSummaryScrollViewBinder>();

            GameObject closeBtn = new("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(dialog.transform, false);
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 16f);
            closeRect.sizeDelta = new Vector2(220f, 40f);
            closeBtn.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.75f, 1f);
            GameObject closeLabel = CreateText(closeBtn.transform, "Text", "Close", 20, FontStyles.Bold);
            Stretch(closeLabel.GetComponent<RectTransform>());
            closeLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            MatchOutcomeSummaryPanelController controller = root.AddComponent<MatchOutcomeSummaryPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue = root;
            so.FindProperty("outcomeTitleText").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();
            so.FindProperty("scrollBinder").objectReferenceValue = binder;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject binderSo = new SerializedObject(binder);
            binderSo.FindProperty("contentRoot").objectReferenceValue = content.transform;
            binderSo.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            Selection.activeGameObject = root;

            Debug.Log(
                "[MatchOutcome] Đã tạo Panel Match Outcome. Gán StatChip prefab (SummaryRow Template) vào Scroll Binder nếu cần.",
                root);
        }

        static GameObject CreateText(Transform parent, string name, string text, int fontSize, FontStyles style)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            return go;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
