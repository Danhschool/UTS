using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Tham chiếu camera/RT fog explored + vision dùng chung cho minimap overlay và icon.
    /// </summary>
    public class MinimapFogSystemReference : MonoBehaviour
    {
        [SerializeField] private Camera exploredFogCamera;
        [SerializeField] private RenderTexture exploredTexture;
        [SerializeField] private Camera visionFogCamera;
        [SerializeField] private RenderTexture visionTexture;
        [SerializeField] private float exploredThreshold = 0.1f;
        [SerializeField] private float visionThreshold = 0.9f;

        private Texture2D exploredCache;
        private Texture2D visionCache;
        private Rect exploredCacheRect;
        private Rect visionCacheRect;

        public Texture2D ExploredCache => exploredCache;
        public Texture2D VisionCache => visionCache;
        public RenderTexture ExploredTexture => exploredTexture;
        public RenderTexture VisionTexture => visionTexture;
        public float ExploredThreshold => exploredThreshold;
        public float VisionThreshold => visionThreshold;
        public Camera ExploredFogCamera => exploredFogCamera;
        public Camera VisionFogCamera => visionFogCamera;

        private void Awake()
        {
            EnsureReferences();
        }

        [SerializeField, Min(1)] private int cacheReadIntervalFrames = 2;
        private int lastExploredReadFrame = -1;
        private int lastVisionReadFrame = -1;
        private bool useFactionRegistryCache;

        private void LateUpdate()
        {
            if (useFactionRegistryCache
                && FactionFogSystemsRegistry.TryGet(trackedFactionOwner, out IFogMapQuery query)
                && query is FactionFogSystemReference)
            {
                return;
            }

            int interval = Mathf.Max(1, cacheReadIntervalFrames);
            int frame = Time.frameCount;
            if (lastExploredReadFrame < 0 || frame - lastExploredReadFrame >= interval)
            {
                RefreshExploredCache();
                lastExploredReadFrame = frame;
            }

            if (lastVisionReadFrame < 0 || frame - lastVisionReadFrame >= interval)
            {
                RefreshVisionCache();
                lastVisionReadFrame = frame;
            }
        }

        Owner trackedFactionOwner = Owner.Player1;

        /// <summary>
        /// Mục tiêu: Minimap sample đúng RT fog nhánh P1/P2 đang active.
        /// Cách hoạt động: Copy camera + RT từ <see cref="FactionFogSystemReference"/> đã đăng ký registry.
        /// </summary>
        public void BindFromFactionFog(FactionFogSystemReference factionFog)
        {
            if (factionFog == null)
            {
                useFactionRegistryCache = false;
                return;
            }

            factionFog.EnsureReferences();
            exploredFogCamera = factionFog.ExploredFogCamera;
            exploredTexture = factionFog.ExploredRenderTexture;
            visionFogCamera = factionFog.VisionFogCamera;
            visionTexture = factionFog.VisionRenderTexture;
            trackedFactionOwner = factionFog.FactionOwner;
            useFactionRegistryCache = true;
        }

        public void EnsureReferences()
        {
            if (exploredFogCamera == null || visionFogCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || camera.targetTexture == null)
                    {
                        continue;
                    }

                    string cameraName = camera.gameObject.name;
                    if (exploredFogCamera == null && cameraName.Contains("Explored"))
                    {
                        exploredFogCamera = camera;
                        exploredTexture = camera.targetTexture;
                    }
                    else if (visionFogCamera == null
                             && (cameraName.Contains("Visibility") || cameraName.Contains("Vision")))
                    {
                        visionFogCamera = camera;
                        visionTexture = camera.targetTexture;
                    }
                }
            }

            if (exploredTexture == null && exploredFogCamera != null)
            {
                exploredTexture = exploredFogCamera.targetTexture;
            }

            if (visionTexture == null && visionFogCamera != null)
            {
                visionTexture = visionFogCamera.targetTexture;
            }
        }

        private void RefreshExploredCache()
        {
            RefreshTextureCache(exploredTexture, ref exploredCache, ref exploredCacheRect);
        }

        private void RefreshVisionCache()
        {
            RefreshTextureCache(visionTexture, ref visionCache, ref visionCacheRect);
        }

        private static void RefreshTextureCache(RenderTexture source, ref Texture2D cache, ref Rect cacheRect)
        {
            if (source == null)
            {
                return;
            }

            if (cache == null || cache.width != source.width || cache.height != source.height)
            {
                cache = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                cacheRect = new Rect(0f, 0f, source.width, source.height);
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            cache.ReadPixels(cacheRect, 0, 0);
            cache.Apply(false, false);
            RenderTexture.active = previous;
        }

        /// <summary>
        /// Mục tiêu: Kiểm tra world XZ đã được explore chưa (cùng RT với fog gameplay).
        /// Cách hoạt động: Map world → UV ortho fog camera, đọc từ cache explored đã cập nhật mỗi frame.
        /// </summary>
        public bool IsWorldPositionExplored(Vector3 worldPosition)
        {
            if (useFactionRegistryCache
                && FactionFogSystemsRegistry.TryGet(trackedFactionOwner, out IFogMapQuery query))
            {
                return query.IsWorldExplored(worldPosition);
            }

            if (exploredFogCamera == null || exploredCache == null)
            {
                return false;
            }

            if (!TryWorldToFogUv(exploredFogCamera, worldPosition, out Vector2 uv))
            {
                return false;
            }

            return exploredCache.GetPixelBilinear(uv.x, uv.y).r > exploredThreshold;
        }

        /// <summary>
        /// Mục tiêu: Điểm world đang trong tầm nhìn hiện tại (vision RT).
        /// Cách hoạt động: Ưu tiên registry faction; fallback cache local.
        /// </summary>
        public bool IsWorldPositionVisible(Vector3 worldPosition)
        {
            if (useFactionRegistryCache
                && FactionFogSystemsRegistry.TryGet(trackedFactionOwner, out IFogMapQuery query))
            {
                return query.IsWorldVisible(worldPosition);
            }

            if (visionFogCamera == null || visionCache == null)
            {
                return false;
            }

            if (!TryWorldToFogUv(visionFogCamera, worldPosition, out Vector2 uv))
            {
                return false;
            }

            return visionCache.GetPixelBilinear(uv.x, uv.y).r > visionThreshold;
        }

        public bool IsFogReady => exploredFogCamera != null && exploredCache != null && visionFogCamera != null && visionCache != null;

        public bool TryWorldToExploredUv(Vector3 worldPosition, out Vector2 uv)
        {
            return TryWorldToFogUv(exploredFogCamera, worldPosition, out uv);
        }

        public bool TryWorldToVisionUv(Vector3 worldPosition, out Vector2 uv)
        {
            return TryWorldToFogUv(visionFogCamera, worldPosition, out uv);
        }

        /// <summary>
        /// Mục tiêu: Map world XZ sang UV 0–1 của camera fog ortho nhìn từ trên.
        /// Cách hoạt động: Chiếu offset world lên right/up camera; không phụ thuộc WorldToScreenPoint.
        /// </summary>
        public static bool TryWorldToFogUv(Camera fogCamera, Vector3 worldPosition, out Vector2 uv) =>
            FogOrthographicUvUtility.TryWorldToFogUv(fogCamera, worldPosition, out uv);

        private void OnDestroy()
        {
            if (exploredCache != null)
            {
                Destroy(exploredCache);
            }

            if (visionCache != null)
            {
                Destroy(visionCache);
            }
        }
    }
}
