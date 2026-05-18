using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    [CreateAssetMenu(fileName = "Minimap Map Bounds", menuName = "RTS/Minimap/Map Bounds")]
    public class MinimapMapBoundsSO : ScriptableObject
    {
        [SerializeField] private float worldMinX = -90f;
        [SerializeField] private float worldMinZ = -90f;
        [SerializeField] private float worldMaxX = 90f;
        [SerializeField] private float worldMaxZ = 90f;

        public float WorldMinX => worldMinX;
        public float WorldMinZ => worldMinZ;
        public float WorldMaxX => worldMaxX;
        public float WorldMaxZ => worldMaxZ;

        /// <summary>
        /// Mục tiêu: Chuyển world XZ sang UV minimap (0–1) — cùng hệ với camera minimap ortho.
        /// Cách hoạt động: Nội suy tuyến tính theo biên; click/icon/viewport dùng chung công thức này.
        /// </summary>
        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            float width = worldMaxX - worldMinX;
            float depth = worldMaxZ - worldMinZ;
            float u = width > 0.0001f ? (worldPosition.x - worldMinX) / width : 0.5f;
            float v = depth > 0.0001f ? (worldPosition.z - worldMinZ) / depth : 0.5f;
            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        /// <summary>
        /// Mục tiêu: Chuyển UV minimap về world XZ (Y = 0) khi click di chuyển camera.
        /// Cách hoạt động: Nội suy ngược theo biên đã sync từ MinimapRenderCamera.
        /// </summary>
        public Vector3 NormalizedToWorld(Vector2 normalized)
        {
            return new Vector3(
                Mathf.Lerp(worldMinX, worldMaxX, normalized.x),
                0f,
                Mathf.Lerp(worldMinZ, worldMaxZ, normalized.y));
        }

        public void SetBounds(float minX, float minZ, float maxX, float maxZ)
        {
            worldMinX = minX;
            worldMinZ = minZ;
            worldMaxX = maxX;
            worldMaxZ = maxZ;
        }

        /// <summary>
        /// Mục tiêu: Cập nhật biên map từ camera minimap ortho (world = WYSIWYG trên UI).
        /// Cách hoạt động: halfHeight = orthoSize; halfWidth = orthoSize × aspect RT.
        /// </summary>
        public void SyncFromOrthographicCamera(Camera camera, RenderTexture targetTexture)
        {
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            float aspect = targetTexture != null && targetTexture.height > 0
                ? (float)targetTexture.width / targetTexture.height
                : camera.aspect;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * aspect;
            Vector3 center = camera.transform.position;

            SetBounds(
                center.x - halfWidth,
                center.z - halfHeight,
                center.x + halfWidth,
                center.z + halfHeight);
        }
    }
}
