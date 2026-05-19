using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Tutorial-style chat scroll: when Content children change, scroll to the newest line at the bottom.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameEventLogScrollController : MonoBehaviour
    {
        private ScrollRect scrollRect;
        private bool autoScrollToBottom = true;
        private Coroutine scrollRoutine;

        public void Initialize(ScrollRect scroll, bool autoScroll)
        {
            scrollRect = scroll;
            autoScrollToBottom = autoScroll;
        }

        private void OnTransformChildrenChanged()
        {
            if (autoScrollToBottom)
            {
                RequestScrollToBottom();
            }
        }

        public void SetAutoScroll(bool enabled)
        {
            autoScrollToBottom = enabled;
        }

        /// <summary>
        /// Mục tiêu: Cuộn xuống tin nhắn mới nhất (pattern ScrollRect chat Unity).
        /// Cách hoạt động: ForceUpdateCanvases sau layout, gán verticalNormalizedPosition = 0.
        /// </summary>
        public void RequestScrollToBottom()
        {
            if (!autoScrollToBottom || scrollRect == null || !isActiveAndEnabled)
            {
                return;
            }

            if (scrollRoutine != null)
            {
                StopCoroutine(scrollRoutine);
            }

            scrollRoutine = StartCoroutine(ScrollToBottomRoutine());
        }

        private IEnumerator ScrollToBottomRoutine()
        {
            yield return null;

            Canvas.ForceUpdateCanvases();

            RectTransform content = scrollRect.content;
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            scrollRect.verticalNormalizedPosition = 0f;

            scrollRoutine = null;
        }
    }
}
