using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    public class ProgressBar : MonoBehaviour
    {
        const string ProgressChildName = "Progress";

        [SerializeField] private Vector2 padding = new (9, 8);
        [SerializeField] private RectTransform mask;
        RectTransform maskParentRectTransform;
        RectTransform progressFillRect;

        void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// Mục tiêu: cập nhật vùng mask theo tiến độ 0–1 để lộ phần fill bên trong.
        /// Cách hoạt động: ép layout, căn fill sát trái full bề rộng track, rồi thu mask từ trái sang phải.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (!CacheReferences())
            {
                return;
            }

            float progress01 = Mathf.Clamp01(progress);
            PrepareLayoutForProgress();

            Vector2 parentSize = maskParentRectTransform.rect.size;
            if (parentSize.x <= 0.01f || parentSize.y <= 0.01f)
            {
                parentSize = maskParentRectTransform.sizeDelta;
            }

            float innerWidth = Mathf.Max(0f, parentSize.x - padding.x * 2f);
            float fillWidth = innerWidth * progress01;

            AlignProgressFill(innerWidth);
            ApplyMaskWidth(fillWidth);
        }

        bool CacheReferences()
        {
            if (mask == null)
            {
                Debug.LogError($"Progress bar {name} is missing a mask! This progress bar will not work!");
                return false;
            }

            if (maskParentRectTransform == null)
            {
                maskParentRectTransform = mask.parent != null ? mask.parent.GetComponent<RectTransform>() : null;
            }

            if (progressFillRect == null)
            {
                Transform progressTransform = mask.Find(ProgressChildName);
                if (progressTransform != null)
                {
                    progressFillRect = progressTransform.GetComponent<RectTransform>();
                }
            }

            return maskParentRectTransform != null;
        }

        void PrepareLayoutForProgress()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(maskParentRectTransform);

            if (maskParentRectTransform.parent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        void AlignProgressFill(float innerWidth)
        {
            if (progressFillRect == null)
            {
                return;
            }

            progressFillRect.anchorMin = new Vector2(0f, 0f);
            progressFillRect.anchorMax = new Vector2(0f, 1f);
            progressFillRect.pivot = new Vector2(0f, 0.5f);
            progressFillRect.anchoredPosition = Vector2.zero;
            progressFillRect.sizeDelta = new Vector2(Mathf.Max(0f, innerWidth), 0f);
        }

        void ApplyMaskWidth(float fillWidth)
        {
            Vector2 parentSize = maskParentRectTransform.rect.size;
            if (parentSize.x <= 0.01f || parentSize.y <= 0.01f)
            {
                parentSize = maskParentRectTransform.sizeDelta;
            }

            mask.anchorMin = Vector2.zero;
            mask.anchorMax = Vector2.one;
            mask.pivot = new Vector2(0f, 0.5f);
            mask.offsetMin = new Vector2(padding.x, padding.y);
            mask.offsetMax = new Vector2(padding.x + fillWidth - parentSize.x, -padding.y);
        }
    }
}
