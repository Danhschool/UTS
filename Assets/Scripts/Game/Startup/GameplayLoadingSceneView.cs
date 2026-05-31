using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: UI loading scene — fill bar Image (không dùng ProgressBar mask).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayLoadingSceneView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Image fillImage;
        [SerializeField] string defaultStatus = "Đang tải…";

        string lastStatus = string.Empty;

        public static GameplayLoadingSceneView CreateRuntime(Transform parent = null)
        {
            var root = new GameObject(nameof(GameplayLoadingSceneView));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var view = root.AddComponent<GameplayLoadingSceneView>();

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var panel = CreateStretchChild(root.transform, "Panel");
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);

            var titleGo = CreateAnchoredChild(panel, "Title", new Vector2(0.5f, 0.58f), new Vector2(900f, 64f));
            var title = titleGo.gameObject.AddComponent<TextMeshProUGUI>();
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 40f;
            title.text = "Đang tải trận đấu";
            title.color = Color.white;

            var statusGo = CreateAnchoredChild(panel, "Status", new Vector2(0.5f, 0.48f), new Vector2(900f, 40f));
            var status = statusGo.gameObject.AddComponent<TextMeshProUGUI>();
            status.alignment = TextAlignmentOptions.Center;
            status.fontSize = 22f;
            status.color = new Color(0.82f, 0.86f, 0.92f, 1f);

            var trackGo = CreateAnchoredChild(panel, "BarTrack", new Vector2(0.5f, 0.38f), new Vector2(640f, 24f));
            var trackImage = trackGo.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.15f, 0.17f, 0.22f, 1f);

            var fillGo = CreateStretchChild(trackGo, "BarFill");
            var fill = fillGo.gameObject.AddComponent<Image>();
            fill.color = new Color(0.35f, 0.72f, 0.95f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            view.canvasGroup = root.AddComponent<CanvasGroup>();
            view.statusText = status;
            view.fillImage = fill;
            return view;
        }

        public void SetStatus(string status)
        {
            if (statusText == null)
            {
                return;
            }

            string resolved = string.IsNullOrWhiteSpace(status) ? defaultStatus : status;
            if (resolved == lastStatus)
            {
                return;
            }

            lastStatus = resolved;
            statusText.SetText(resolved);
        }

        /// <summary>
        /// Mục tiêu: Cập nhật fill 0–1 cho thanh loading.
        /// </summary>
        public void SetProgress01(float normalized01)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = Mathf.Clamp01(normalized01);
        }

        static RectTransform CreateStretchChild(Transform parent, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform CreateAnchoredChild(RectTransform parent, string objectName, Vector2 anchorY, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorY;
            rect.anchorMax = anchorY;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }
    }
}
