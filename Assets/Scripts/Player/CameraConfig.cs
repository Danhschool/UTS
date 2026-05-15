using UnityEngine;

namespace GameDevTV.RTS.Player
{
    [System.Serializable]
    public class CameraConfig
    {
        [Header("Pan — viền màn hình (chuột)")]
        [field: SerializeField, Tooltip("Bật di chuyển camera khi đưa chuột vào vùng sát mép màn hình.")]
        public bool EnableEdgePan { get; private set; } = true;

        [field: SerializeField, Tooltip("Khoảng cách từ mép màn hình (pixel): chuột trong vùng này sẽ pan. Tăng nếu phải đưa chuột quá sát viền mới kéo được.")]
        [field: Range(8f, 500f)]
        public float EdgePanSize { get; private set; } = 80f;

        [field: SerializeField, Tooltip("Nếu bật: vùng pan = max(Edge Pan Size pixel, % chiều ngắn màn hình). Hữu ích trên màn hình lớn / 4K.")]
        public bool UseEdgePanScreenPercent { get; private set; }

        [field: SerializeField, Tooltip("Phần trăm chiều ngắn màn hình (0.05 = 5%) dùng khi Use Edge Pan Screen Percent bật.")]
        [field: Range(0.005f, 0.25f)]
        public float EdgePanScreenPercent { get; private set; } = 0.05f;

        [field: SerializeField, Tooltip("Tốc độ pan khi chuột ở vùng viền.")]
        public float MousePanSpeed { get; private set; } = 5;

        [Header("Pan — phím mũi tên")]
        [field: SerializeField] public float KeyboardPanSpeed { get; private set; } = 5;

        [field: SerializeField] public bool EnablePanLimits { get; private set; }
        [field: SerializeField] public Vector2 PanLimitMinXZ { get; private set; } = new(-500f, -500f);
        [field: SerializeField] public Vector2 PanLimitMaxXZ { get; private set; } = new(500f, 500f);

        [field: SerializeField] public float ZoomSpeed { get; private set; } = 1;
        /// <summary>Giới hạn zoom theo tỉ lệ so với FollowOffset Y/Z ban đầu (1 = mặc định). Nhỏ hơn = gần mặt đất hơn, cảm giác phóng to.</summary>
        [field: SerializeField] public float MinZoomScale { get; private set; } = 0.45f;
        [field: SerializeField] public float MaxZoomScale { get; private set; } = 1.65f;
        [field: SerializeField] public float ScrollZoomSensitivity { get; private set; } = 0.12f;

        [field: SerializeField] public float MinOrthographicSize { get; private set; } = 8f;
        [field: SerializeField] public float MaxOrthographicSize { get; private set; } = 40f;

        [field: SerializeField] public float RotationSpeed { get; private set; } = 1;

        /// <summary>
        /// Mục tiêu: trả về độ rộng vùng viền (pixel) dùng cho edge pan, có thể lớn hơn <see cref="EdgePanSize"/> khi bật % màn hình.
        /// Cách hoạt động: lấy <see cref="EdgePanSize"/>; nếu <see cref="UseEdgePanScreenPercent"/> thì max với (chiều ngắn màn hình × <see cref="EdgePanScreenPercent"/>).
        /// </summary>
        public float GetEdgePanBorderPixels()
        {
            float border = EdgePanSize;
            if (UseEdgePanScreenPercent)
            {
                float fromPercent = Mathf.Min(Screen.width, Screen.height) * EdgePanScreenPercent;
                border = Mathf.Max(border, fromPercent);
            }

            return border;
        }
    }
}