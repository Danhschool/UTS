using UnityEngine;

namespace GameDevTV.RTS.Player
{
    [System.Serializable]
    public class CameraConfig
    {
        [field: SerializeField] public bool EnableEdgePan { get; private set; } = true;
        [field: SerializeField] public float MousePanSpeed { get; private set; } = 5;
        [field: SerializeField] public float EdgePanSize { get; private set; } = 50;

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
    }
}