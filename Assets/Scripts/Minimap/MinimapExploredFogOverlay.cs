using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Lớp UI fog minimap: đen (chưa explore), mờ (explored), trong suốt (đang nhìn thấy).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MinimapExploredFogOverlay : MonoBehaviour
    {
        private static readonly int ExploredTexId = Shader.PropertyToID("_ExploredTex");
        private static readonly int VisionTexId = Shader.PropertyToID("_VisionTex");
        private static readonly int UnexploredColorId = Shader.PropertyToID("_UnexploredColor");
        private static readonly int ExploredFogColorId = Shader.PropertyToID("_ExploredFogColor");
        private static readonly int ExploredThresholdId = Shader.PropertyToID("_ExploredThreshold");
        private static readonly int VisionThresholdId = Shader.PropertyToID("_VisionThreshold");
        private static readonly int FogUvBottomLeftId = Shader.PropertyToID("_FogUvBottomLeft");
        private static readonly int FogUvTopRightId = Shader.PropertyToID("_FogUvTopRight");

        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MinimapFogSystemReference fogSystem;
        [SerializeField] private Color unexploredColor = Color.black;
        [SerializeField] private Color exploredFogColor = new(0f, 0f, 0f, 0.75f);
        [SerializeField] private Shader overlayShader;

        private RawImage rawImage;
        private Material overlayMaterial;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            fogSystem?.EnsureReferences();
            EnsureMaterial();
        }

        private void LateUpdate()
        {
            if (mapBounds == null || fogSystem == null || overlayMaterial == null)
            {
                return;
            }

            fogSystem.EnsureReferences();

            Texture2D explored = fogSystem.ExploredCache;
            Texture2D vision = fogSystem.VisionCache;
            if (explored == null)
            {
                rawImage.enabled = false;
                return;
            }

            rawImage.enabled = true;
            overlayMaterial.SetTexture(ExploredTexId, explored);
            overlayMaterial.SetColor(UnexploredColorId, unexploredColor);
            overlayMaterial.SetColor(ExploredFogColorId, exploredFogColor);
            overlayMaterial.SetFloat(ExploredThresholdId, fogSystem.ExploredThreshold);

            if (vision != null)
            {
                overlayMaterial.SetTexture(VisionTexId, vision);
                overlayMaterial.SetFloat(VisionThresholdId, fogSystem.VisionThreshold);
            }
            else
            {
                overlayMaterial.SetTexture(VisionTexId, Texture2D.blackTexture);
                overlayMaterial.SetFloat(VisionThresholdId, 1.1f);
            }

            Vector3 worldBottomLeft = mapBounds.NormalizedToWorld(Vector2.zero);
            Vector3 worldTopRight = mapBounds.NormalizedToWorld(Vector2.one);

            if (fogSystem.TryWorldToExploredUv(worldBottomLeft, out Vector2 fogUvBottomLeft)
                && fogSystem.TryWorldToExploredUv(worldTopRight, out Vector2 fogUvTopRight))
            {
                overlayMaterial.SetVector(FogUvBottomLeftId, new Vector4(fogUvBottomLeft.x, fogUvBottomLeft.y, 0f, 0f));
                overlayMaterial.SetVector(FogUvTopRightId, new Vector4(fogUvTopRight.x, fogUvTopRight.y, 0f, 0f));
            }
        }

        public void Configure(MinimapMapBoundsSO bounds, MinimapFogSystemReference reference)
        {
            mapBounds = bounds;
            fogSystem = reference;
        }

        private void EnsureMaterial()
        {
            if (overlayShader == null)
            {
                overlayShader = Shader.Find("RTS/Minimap/Explored Fog Overlay");
            }

            if (overlayShader == null)
            {
                Debug.LogWarning("MinimapExploredFogOverlay: không tìm thấy shader RTS/Minimap/Explored Fog Overlay.", this);
                return;
            }

            overlayMaterial = new Material(overlayShader);
            rawImage.material = overlayMaterial;
            rawImage.texture = null;
            rawImage.raycastTarget = false;
            rawImage.color = Color.white;
        }

        private void OnDestroy()
        {
            if (overlayMaterial != null)
            {
                Destroy(overlayMaterial);
            }
        }
    }
}
