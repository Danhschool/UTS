using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Shared parabolic arc math for unit/building projectiles.
    /// </summary>
    public static class ProjectileArcMath
    {
        /// <summary>
        /// Mục tiêu: Điểm trên vòng cung parabol tại t ∈ [0, 1].
        /// Cách hoạt động: Lerp start→goal; cộng 4t(1−t)×arcHeight (đỉnh giữa đường).
        /// </summary>
        public static Vector3 EvaluatePosition(Vector3 start, Vector3 goal, float t, float arcHeight)
        {
            Vector3 position = Vector3.Lerp(start, goal, t);
            float lift = 4f * t * (1f - t);
            position.y += lift * arcHeight;
            return position;
        }

        /// <summary>
        /// Mục tiêu: Ước lượng độ dài vòng cung để tính thời gian bay.
        /// Cách hoạt động: Lấy mẫu 12 điểm dọc đường cong và cộng khoảng cách đoạn.
        /// </summary>
        public static float EstimateArcLength(Vector3 start, Vector3 goal, float arcHeight)
        {
            const int samples = 12;
            float length = 0f;
            Vector3 previous = start;

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 sample = EvaluatePosition(start, goal, t, arcHeight);
                length += Vector3.Distance(previous, sample);
                previous = sample;
            }

            return length;
        }
    }
}
