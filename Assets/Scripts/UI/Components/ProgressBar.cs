using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField] private Vector2 padding = new (9, 8);
        [SerializeField] private RectTransform mask;
        private RectTransform maskParentRectTransform;

        private void Awake()
        {
            if (mask == null)
            {
                Debug.LogError($"Progress bar {name} is missing a mask! This progress bar will not work!");
                return;
            }

            maskParentRectTransform = mask.parent.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Mục tiêu: cập nhật vùng mask theo tiến độ 0–1 để lộ phần fill bên trong.
        /// Cách hoạt động: lấy kích thước thật của RectTransform cha bằng <see cref="RectTransform.rect"/> (đúng cả khi anchors stretch);
        /// nếu layout chưa chạy (rect ~ 0) thì ép rebuild một lần rồi tính lại offset cho Mask.
        /// </summary>
        public void SetProgress(float progress)
        {
            if (mask == null)
            {
                return;
            }

            if (maskParentRectTransform == null)
            {
                maskParentRectTransform = mask.parent != null ? mask.parent.GetComponent<RectTransform>() : null;
                if (maskParentRectTransform == null)
                {
                    return;
                }
            }

            Vector2 parentSize = maskParentRectTransform.rect.size;
            if (parentSize.sqrMagnitude < 0.0001f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(maskParentRectTransform);
                parentSize = maskParentRectTransform.rect.size;
            }

            Vector2 targetSize = parentSize - padding * 2;
            targetSize.x *= Mathf.Clamp01(progress);

            mask.offsetMin = padding;
            mask.offsetMax = new Vector2(padding.x + targetSize.x - parentSize.x, -padding.y);
        }
    }
}
