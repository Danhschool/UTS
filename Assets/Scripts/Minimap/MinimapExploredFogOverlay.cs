using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Lớp UI fog minimap: đen (chưa explore), mờ (explored), trong suốt (đang nhìn thấy).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [DefaultExecutionOrder(200)]
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
        private static readonly int VisionFogUvBottomLeftId = Shader.PropertyToID("_VisionFogUvBottomLeft");
        private static readonly int VisionFogUvTopRightId = Shader.PropertyToID("_VisionFogUvTopRight");

        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MinimapFogSystemReference fogSystem;
        [SerializeField] private Owner fogFactionOwner = Owner.Player1;
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
            if (mapBounds == null || overlayMaterial == null)
            {
                SetOverlayVisible(false);
                return;
            }

            bool resolved = HumanFogVisionUtility.EmitsFogVision(fogFactionOwner)
                ? MinimapFactionFogResolver.TryResolveFactionFog(
                    fogFactionOwner,
                    out FactionFogSystemReference factionFog,
                    out Camera fogUvCamera)
                : MinimapFactionFogResolver.TryResolveLocalFactionFog(
                    out factionFog,
                    out fogUvCamera);

            if (!resolved)
            {
                SetOverlayVisible(false);
                return;
            }

            if (fogSystem != null)
            {
                fogSystem.BindFromFactionFog(factionFog);
            }

            Camera exploredCamera = factionFog.ExploredFogCamera ?? fogUvCamera;
            Camera visionCamera = factionFog.VisionFogCamera ?? fogUvCamera;

            if (!TryComputeFogUvRect(exploredCamera, out Vector2 exploredUvBottomLeft, out Vector2 exploredUvTopRight)
                || !TryComputeFogUvRect(visionCamera, out Vector2 visionUvBottomLeft, out Vector2 visionUvTopRight))
            {
                SetOverlayVisible(false);
                return;
            }

            float exploredCutoff = factionFog.ExploredThreshold;
            float visionCutoff = factionFog.VisionThreshold;

            SetOverlayVisible(true);
            overlayMaterial.SetTexture(ExploredTexId, factionFog.ExploredRenderTexture);
            overlayMaterial.SetTexture(VisionTexId, factionFog.VisionRenderTexture);
            overlayMaterial.SetColor(UnexploredColorId, unexploredColor);
            overlayMaterial.SetColor(ExploredFogColorId, exploredFogColor);
            overlayMaterial.SetFloat(ExploredThresholdId, exploredCutoff);
            overlayMaterial.SetFloat(VisionThresholdId, visionCutoff);
            overlayMaterial.SetVector(FogUvBottomLeftId, new Vector4(exploredUvBottomLeft.x, exploredUvBottomLeft.y, 0f, 0f));
            overlayMaterial.SetVector(FogUvTopRightId, new Vector4(exploredUvTopRight.x, exploredUvTopRight.y, 0f, 0f));
            overlayMaterial.SetVector(VisionFogUvBottomLeftId, new Vector4(visionUvBottomLeft.x, visionUvBottomLeft.y, 0f, 0f));
            overlayMaterial.SetVector(VisionFogUvTopRightId, new Vector4(visionUvTopRight.x, visionUvTopRight.y, 0f, 0f));
        }

        /// <summary>
        /// Mục tiêu: Map rect minimap (world bounds) sang UV fog ortho — không bỏ qua khi góc ngoài 0–1.
        /// Cách hoạt động: NormalizedToWorld(0,0)/(1,1) rồi TryWorldToFogUvUnclamped.
        /// </summary>
        bool TryComputeFogUvRect(Camera fogCamera, out Vector2 bottomLeft, out Vector2 topRight)
        {
            bottomLeft = Vector2.zero;
            topRight = Vector2.one;

            Vector3 worldBottomLeft = mapBounds.NormalizedToWorld(Vector2.zero);
            Vector3 worldTopRight = mapBounds.NormalizedToWorld(Vector2.one);

            return FogOrthographicUvUtility.TryWorldToFogUvUnclamped(fogCamera, worldBottomLeft, out bottomLeft)
                   && FogOrthographicUvUtility.TryWorldToFogUvUnclamped(fogCamera, worldTopRight, out topRight);
        }

        void SetOverlayVisible(bool visible)
        {
            if (rawImage != null)
            {
                rawImage.enabled = visible;
            }
        }

        public void Configure(MinimapMapBoundsSO bounds, MinimapFogSystemReference reference, Owner factionOwner)
        {
            mapBounds = bounds;
            fogSystem = reference;
            if (HumanFogVisionUtility.EmitsFogVision(factionOwner))
            {
                fogFactionOwner = factionOwner;
            }
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
