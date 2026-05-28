using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// Trượt Image trái/phải trong khoảng ±Move Distance. Lớp 1 đứng yên, lớp 2 = X% tốc độ, lớp 3 = 100%.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParallaxMenuBackgroundController : MonoBehaviour
    {
        [SerializeField] Image[] backgroundLayers;
        [SerializeField] float moveSpeed = 50f;
        [SerializeField] float moveDistance = 100f;
        [SerializeField, Range(0f, 100f)] float layer2SpeedPercent = 50f;

        Vector2[] startPositions;

        void Awake()
        {
            CacheStartPositions();
        }

        void Update()
        {
            if (backgroundLayers == null || backgroundLayers.Length == 0 || moveDistance <= 0f)
            {
                return;
            }

            float time = Time.unscaledTime;

            for (int i = 0; i < backgroundLayers.Length; i++)
            {
                Image image = backgroundLayers[i];
                if (image == null)
                {
                    continue;
                }

                float offsetX = GetOffsetX(time, i);
                image.rectTransform.anchoredPosition = startPositions[i] + new Vector2(offsetX, 0f);
            }
        }

        /// <summary>
        /// Mục tiêu: giới hạn dịch chuyển trong [-moveDistance, +moveDistance].
        /// Cách hoạt động: PingPong theo thời gian × tốc độ lớp, rồi căn về khoảng ±X.
        /// </summary>
        float GetOffsetX(float time, int layerIndex)
        {
            float speedMultiplier = GetSpeedMultiplier(layerIndex);
            if (speedMultiplier <= 0f)
            {
                return 0f;
            }

            float ping = Mathf.PingPong(time * moveSpeed * speedMultiplier, moveDistance * 2f);
            return ping - moveDistance;
        }

        float GetSpeedMultiplier(int layerIndex)
        {
            if (layerIndex == 0)
            {
                return 0f;
            }

            if (layerIndex == 1)
            {
                return layer2SpeedPercent / 100f;
            }

            return 1f;
        }

        void CacheStartPositions()
        {
            if (backgroundLayers == null)
            {
                startPositions = System.Array.Empty<Vector2>();
                return;
            }

            startPositions = new Vector2[backgroundLayers.Length];
            for (int i = 0; i < backgroundLayers.Length; i++)
            {
                Image image = backgroundLayers[i];
                startPositions[i] = image != null
                    ? image.rectTransform.anchoredPosition
                    : Vector2.zero;
            }
        }
    }
}
