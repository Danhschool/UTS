using UnityEngine;

namespace GameDevTV.RTS.Units.Visualization
{
    /// <summary>
    /// Tạo điểm vòng tròn trên mặt phẳng XZ (tầm đánh trên map).
    /// </summary>
    public static class AttackRangeCircleUtility
    {
        /// <summary>
        /// Mục tiêu: Sinh các điểm đều trên chu vi để LineRenderer/Gizmos vẽ vòng.
        /// Cách hoạt động: Bước góc 2π/segmentCount, quy đổi cos/sin ra offset XZ quanh tâm.
        /// </summary>
        public static void FillCircleXZ(
            Vector3[] buffer,
            Vector3 center,
            float radius,
            float yOffset = 0.05f)
        {
            if (buffer == null || buffer.Length < 3)
            {
                return;
            }

            int count = buffer.Length;
            float step = Mathf.PI * 2f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = step * i;
                buffer[i] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + yOffset,
                    center.z + Mathf.Sin(angle) * radius);
            }
        }

        /// <summary>
        /// Mục tiêu: Vẽ vòng tầm đánh trong Scene view (Gizmos).
        /// Cách hoạt động: Nối các điểm trên chu vi bằng Gizmos.DrawLine.
        /// </summary>
        public static void DrawGizmoCircleXZ(Vector3 center, float radius, Color color, int segments = 48)
        {
            if (radius <= 0f || segments < 3)
            {
                return;
            }

            Vector3[] points = new Vector3[segments];
            FillCircleXZ(points, center, radius);

            Color previous = Gizmos.color;
            Gizmos.color = color;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                Gizmos.DrawLine(points[i], points[next]);
            }

            Gizmos.color = previous;
        }
    }
}
