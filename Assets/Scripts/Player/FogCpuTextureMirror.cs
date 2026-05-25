using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Mirror RenderTexture fog sang Texture2D CPU — ReadPixels có throttle, sample bilinear.
    /// </summary>
    public sealed class FogCpuTextureMirror
    {
        Texture2D cpuTexture;
        Rect readRect;
        int lastReadFrame = -1;
        int sourceInstanceId;

        /// <summary>
        /// Mục tiêu: Cập nhật cache CPU từ RT (tối đa mỗi intervalFrames).
        /// Cách hoạt động: ReadPixels + Apply; bỏ qua nếu cùng frame bucket.
        /// </summary>
        public bool TryRefresh(RenderTexture source, int intervalFrames = 2)
        {
            if (source == null)
            {
                return cpuTexture != null;
            }

            int interval = Mathf.Max(1, intervalFrames);
            int frame = Time.frameCount;
            int id = source.GetInstanceID();
            if (cpuTexture != null
                && sourceInstanceId == id
                && lastReadFrame >= 0
                && frame - lastReadFrame < interval)
            {
                return true;
            }

            EnsureCpuTexture(source);
            if (cpuTexture == null)
            {
                return false;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            cpuTexture.ReadPixels(readRect, 0, 0);
            cpuTexture.Apply(false, false);
            RenderTexture.active = previous;
            lastReadFrame = frame;
            sourceInstanceId = id;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Điểm world có trong vùng sáng trên vision/explored RT không.
        /// Cách hoạt động: World→UV ortho → GetPixelBilinear (không GetPixel từng object).
        /// </summary>
        public bool SampleWorldVisible(
            Camera fogCamera,
            Vector3 worldPosition,
            float threshold,
            bool requireInsideUv = true)
        {
            if (cpuTexture == null || fogCamera == null)
            {
                return false;
            }

            if (!FogOrthographicUvUtility.TryWorldToFogUv(fogCamera, worldPosition, out Vector2 uv))
            {
                return !requireInsideUv;
            }

            return cpuTexture.GetPixelBilinear(uv.x, uv.y).r > threshold;
        }

        /// <summary>
        /// Mục tiêu: Debug/visibility — lấy kênh R tại world (không ngưỡng).
        /// Cách hoạt động: Map UV ortho rồi GetPixelBilinear; trả 0 nếu ngoài frustum.
        /// </summary>
        public float SampleWorldChannel(
            Camera fogCamera,
            Vector3 worldPosition,
            bool requireInsideUv = true)
        {
            if (cpuTexture == null || fogCamera == null)
            {
                return 0f;
            }

            if (!FogOrthographicUvUtility.TryWorldToFogUv(fogCamera, worldPosition, out Vector2 uv))
            {
                return requireInsideUv ? 0f : cpuTexture.GetPixelBilinear(0.5f, 0.5f).r;
            }

            return cpuTexture.GetPixelBilinear(uv.x, uv.y).r;
        }

        public bool HasCpuTexture => cpuTexture != null;

        public void Release()
        {
            if (cpuTexture != null)
            {
                Object.Destroy(cpuTexture);
                cpuTexture = null;
            }

            lastReadFrame = -1;
            sourceInstanceId = 0;
        }

        void EnsureCpuTexture(RenderTexture source)
        {
            if (cpuTexture != null
                && cpuTexture.width == source.width
                && cpuTexture.height == source.height
                && sourceInstanceId == source.GetInstanceID())
            {
                return;
            }

            if (cpuTexture != null)
            {
                Object.Destroy(cpuTexture);
            }

            cpuTexture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            readRect = new Rect(0f, 0f, source.width, source.height);
            sourceInstanceId = source.GetInstanceID();
        }
    }
}
