using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Map world XZ → UV texture fog ortho (dùng chung minimap, visibility, IFogMapQuery).
    /// </summary>
    public static class FogOrthographicUvUtility
    {
        /// <summary>
        /// Mục tiêu: Frustum ortho fog khớp pixel RT (384×512), không dùng aspect màn hình Game view.
        /// Cách hoạt động: Ưu tiên targetTexture.width/height; fallback camera.aspect.
        /// </summary>
        public static float GetEffectiveAspect(Camera fogCamera)
        {
            if (fogCamera == null)
            {
                return 1f;
            }

            RenderTexture targetTexture = fogCamera.targetTexture;
            if (targetTexture != null && targetTexture.height > 0)
            {
                return (float)targetTexture.width / targetTexture.height;
            }

            return fogCamera.aspect;
        }

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

            if (!TryProjectWorldToFogUv(fogCamera, worldPosition, out uv))
            {
                return false;
            }

            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>
        /// Mục tiêu: Map world → UV fog kể cả ngoài frustum (minimap overlay vẫn gán được rect).
        /// Cách hoạt động: Cùng công thức ortho; shader saturate UV — không trả false khi &lt;0 hoặc &gt;1.
        /// </summary>
        public static bool TryWorldToFogUvUnclamped(Camera fogCamera, Vector3 worldPosition, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (fogCamera == null || !fogCamera.orthographic)
            {
                return false;
            }

            return TryProjectWorldToFogUv(fogCamera, worldPosition, out uv);
        }

        static bool TryProjectWorldToFogUv(Camera fogCamera, Vector3 worldPosition, out Vector2 uv)
        {
            Transform cameraTransform = fogCamera.transform;
            Vector3 offset = worldPosition - cameraTransform.position;
            float halfHeight = fogCamera.orthographicSize;
            float halfWidth = halfHeight * GetEffectiveAspect(fogCamera);

            uv.x = Vector3.Dot(offset, cameraTransform.right) / (halfWidth * 2f) + 0.5f;
            uv.y = Vector3.Dot(offset, cameraTransform.up) / (halfHeight * 2f) + 0.5f;
            return true;
        }
    }
}
