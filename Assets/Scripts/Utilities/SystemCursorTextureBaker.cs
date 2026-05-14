using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Chuẩn hóa texture để dùng với <see cref="UnityEngine.Cursor.SetCursor"/>:
    /// asset Sprite/compressed thường không đọc được từ CPU và không đúng RGBA32/mip — Unity sẽ báo lỗi.
    /// </summary>
    public static class SystemCursorTextureBaker
    {
        /// <summary>
        /// Mục tiêu: Tạo một <see cref="Texture2D"/> RGBA32, readable, một mức mip, phù hợp hardware cursor.
        /// Cách hoạt động: <see cref="Graphics.Blit"/> sang <see cref="RenderTexture"/> ARGB32 tạm (GPU),
        /// rồi <see cref="Texture2D.ReadPixels"/> sang texture mới — không cần Read/Write trên asset gốc.
        /// </summary>
        public static Texture2D BakeForSystemCursor(Texture2D source)
        {
            if (source == null || source.width < 1 || source.height < 1)
            {
                return null;
            }

            int w = source.width;
            int h = source.height;

            RenderTextureDescriptor desc = new(w, h, RenderTextureFormat.ARGB32, 0)
            {
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = true
            };

            RenderTexture rt = RenderTexture.GetTemporary(desc);
            // Phải lưu active *trước* Blit: Blit có thể gán RenderTexture.active = rt; nếu previous == rt thì ReleaseTemporary sẽ cảnh báo.
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D baked = null;
            try
            {
                baked = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false)
                {
                    name = source.name + "_SystemCursor",
                    hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInHierarchy
                };
                baked.ReadPixels(new Rect(0, 0, w, h), 0, 0, recalculateMipMaps: false);
                baked.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            RenderTexture.ReleaseTemporary(rt);

            return baked;
        }
    }
}
