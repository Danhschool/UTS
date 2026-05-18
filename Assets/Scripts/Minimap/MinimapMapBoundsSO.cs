using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    [CreateAssetMenu(fileName = "Minimap Map Bounds", menuName = "RTS/Minimap/Map Bounds")]
    public class MinimapMapBoundsSO : ScriptableObject
    {
        [SerializeField] private float worldMinX = -500f;
        [SerializeField] private float worldMinZ = -500f;
        [SerializeField] private float worldMaxX = 500f;
        [SerializeField] private float worldMaxZ = 500f;
        [SerializeField] private bool flipZ;

        public float WorldMinX => worldMinX;
        public float WorldMinZ => worldMinZ;
        public float WorldMaxX => worldMaxX;
        public float WorldMaxZ => worldMaxZ;

        /// <summary>
        /// Mục tiêu: Chuyển vị trí world (XZ) sang tọa độ chuẩn hóa 0–1 trên minimap.
        /// Cách hoạt động: Linear map theo biên; tùy chọn flip trục Z nếu ảnh nền ngược hướng world.
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            float width = worldMaxX - worldMinX;
            float depth = worldMaxZ - worldMinZ;
            float u = width > 0.0001f ? (worldPosition.x - worldMinX) / width : 0.5f;
            float v = depth > 0.0001f ? (worldPosition.z - worldMinZ) / depth : 0.5f;
            if (flipZ)
            {
                v = 1f - v;
            }

            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        /// <summary>
        /// Mục tiêu: Chuyển điểm trên minimap (0–1) về world XZ (Y = 0).
        /// Cách hoạt động: Nội suy ngược theo biên đã cấu hình.
        /// </summary>
        public Vector3 NormalizedToWorld(Vector2 normalized)
        {
            float v = flipZ ? 1f - normalized.y : normalized.y;
            return new Vector3(
                Mathf.Lerp(worldMinX, worldMaxX, normalized.x),
                0f,
                Mathf.Lerp(worldMinZ, worldMaxZ, v));
        }

        public void SetBounds(float minX, float minZ, float maxX, float maxZ)
        {
            worldMinX = minX;
            worldMinZ = minZ;
            worldMaxX = maxX;
            worldMaxZ = maxZ;
        }
    }
}
