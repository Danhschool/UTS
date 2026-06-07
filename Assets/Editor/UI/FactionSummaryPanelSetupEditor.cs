using GameDevTV.RTS.UI.InGame;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Menu helper: tạo hierarchy mẫu cho panel tóm tắt (Tab giữ) trong scene/prefab HUD.
    /// </summary>
    public static class FactionSummaryPanelSetupEditor
    {
        const string MenuPath = "GameDevTV/UI/InGame/Create Faction Summary Panel Template";

        [MenuItem(MenuPath)]
        public static void CreateTemplateUnderSelection()
        {
            Transform parent = Selection.activeTransform;
            if (parent == null)
            {
                EditorUtility.DisplayDialog(
                    "Faction Summary",
                    "Chọn GameObject HUD (Canvas hoặc panel con) rồi chạy lại menu.",
                    "OK");
                return;
            }

            GameObject root = new("Panel Faction Summary", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Faction Summary Panel");
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(420f, 520f);
            rootRect.anchoredPosition = Vector2.zero;

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

            GameObject title = CreateText(root.transform, "Title", "Tóm tắt (giữ Tab)", 22, FontStyles.Bold);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(-24f, 36f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);

            GameObject scroll = new("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(root.transform, false);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(12f, 12f);
            scrollRect.offsetMax = new Vector2(-12f, -52f);
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
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            GameObject rowTemplate = CreateSummaryRow(content.transform, "SummaryRow Template");
            rowTemplate.SetActive(false);

            ScrollRect scrollComponent = scroll.GetComponent<ScrollRect>();
            scrollComponent.viewport = viewport.GetComponent<RectTransform>();
            scrollComponent.content = contentRect;
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;
            scrollComponent.movementType = ScrollRect.MovementType.Clamped;

            FactionSummaryScrollViewBinder binder = scroll.AddComponent<FactionSummaryScrollViewBinder>();
            SerializedObject binderSo = new SerializedObject(binder);
            binderSo.FindProperty("contentRoot").objectReferenceValue = content.transform;
            binderSo.FindProperty("summaryRowPrefab").objectReferenceValue = rowTemplate;
            binderSo.ApplyModifiedPropertiesWithoutUndo();

            FactionSummaryHoldTabController controller = root.AddComponent<FactionSummaryHoldTabController>();
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("summaryPanelRoot").objectReferenceValue = root;
            controllerSo.FindProperty("scrollBinder").objectReferenceValue = binder;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            Selection.activeGameObject = root;

            Debug.Log(
                "[FactionSummary] Đã tạo panel mẫu. Giữ Tab trong Play để xem; panel không pause game. "
                + "Chỉnh font TMP trên row template nếu cần.",
                root);
        }

        static GameObject CreateSummaryRow(Transform parent, string name)
        {
            GameObject row = new(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            LayoutElement layoutElement = row.GetComponent<LayoutElement>();
            layoutElement.minHeight = 28f;
            layoutElement.preferredHeight = 28f;

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 12f;
            rowLayout.padding = new RectOffset(4, 8, 0, 0);
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            GameObject label = CreateText(row.transform, "Label", "Mục", 18, FontStyles.Normal);
            LayoutElement labelLayout = label.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 0f;

            GameObject value = CreateText(row.transform, "Value", "0/0", 18, FontStyles.Normal);
            LayoutElement valueLayout = value.AddComponent<LayoutElement>();
            valueLayout.minWidth = 112f;
            valueLayout.preferredWidth = 112f;
            valueLayout.flexibleWidth = 0f;
            TextMeshProUGUI valueTmp = value.GetComponent<TextMeshProUGUI>();
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;

            row.AddComponent<FactionSummaryItemView>();
            return row;
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
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
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
