using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Map world XZ → UV texture fog ortho (dùng chung minimap, visibility, IFogMapQuery).
    /// </summary>
    public static class FogOrthographicUvUtility
    {
        /// <summary>
        /// Mục tiêu: UV 0–1 trong RT fog nhìn từ trên.
        /// Cách hoạt động: Chiếu offset world lên right/up của camera ortho.
        /// </summary>
        public static bool TryWorldToFogUv(Camera fogCamera, Vector3 worldPosition, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (fogCamera == null || !fogCamera.orthographic)
            {
                return false;
            }

            Transform cameraTransform = fogCamera.transform;
            Vector3 offset = worldPosition - cameraTransform.position;
            float halfHeight = fogCamera.orthographicSize;
            float halfWidth = halfHeight * fogCamera.aspect;

            uv.x = Vector3.Dot(offset, cameraTransform.right) / (halfWidth * 2f) + 0.5f;
            uv.y = Vector3.Dot(offset, cameraTransform.up) / (halfHeight * 2f) + 0.5f;
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }
    }
}
