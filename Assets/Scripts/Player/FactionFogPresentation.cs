using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Bật/tắt một cặp camera fog (explored + visibility) cho Player1 hoặc Player2.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionFogPresentation : MonoBehaviour
    {
        static readonly int ExploredTextureId = Shader.PropertyToID("_Explored_Fog_of_War_Render_Texture");
        static readonly int VisionTextureId = Shader.PropertyToID("_Fog_of_War_Render_Texture");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [SerializeField] Owner presentationOwner = Owner.Player1;
        [SerializeField] GameObject presentationRoot;
        [SerializeField] Camera exploredFogCamera;
        [SerializeField] Camera visionFogCamera;
        [SerializeField] FactionFogSystemReference fogSystemReference;
        [SerializeField] MeshRenderer fogPlaneRenderer;

        public Owner PresentationOwner => presentationOwner;
        public Camera VisionFogCamera => visionFogCamera;
        public FactionFogSystemReference FogSystemReference => fogSystemReference;

        void Awake()
        {
            if (presentationRoot == null)
            {
                presentationRoot = gameObject;
            }

            if (fogSystemReference == null)
            {
                fogSystemReference = GetComponentInChildren<FactionFogSystemReference>(true);
            }

            if (exploredFogCamera == null || visionFogCamera == null)
            {
                Camera[] cameras = GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || camera.targetTexture == null)
                    {
                        continue;
                    }

                    string name = camera.gameObject.name;
                    if (exploredFogCamera == null && name.Contains("Explored"))
                    {
                        exploredFogCamera = camera;
                    }
                    else if (visionFogCamera == null
                             && (name.Contains("Visibility") || name.Contains("Vision")))
                    {
                        visionFogCamera = camera;
                    }
                }
            }

            if (fogSystemReference == null)
            {
                fogSystemReference = GetComponent<FactionFogSystemReference>();
            }

            if (fogPlaneRenderer == null)
            {
                MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].gameObject.name.Contains("Plane"))
                    {
                        fogPlaneRenderer = renderers[i];
                        break;
                    }
                }
            }

            if (fogSystemReference != null)
            {
                fogSystemReference.ConfigureFaction(presentationOwner);
            }
        }

        /// <summary>
        /// Mục tiêu: Chỉ một nhánh fog render trên client (tiết kiệm GPU).
        /// Cách hoạt động: Enable/disable root và cameras; registry qua <see cref="FactionFogSystemReference"/>.
        /// </summary>
        public void SetPresentationActive(bool active)
        {
            if (presentationRoot != null)
            {
                presentationRoot.SetActive(active);
            }

            if (exploredFogCamera != null)
            {
                exploredFogCamera.enabled = active;
            }

            if (visionFogCamera != null)
            {
                visionFogCamera.enabled = active;
            }

            if (fogSystemReference != null)
            {
                fogSystemReference.enabled = active;
            }

            if (active)
            {
                SyncFogPlaneMaterial();
            }
        }

        /// <summary>
        /// Mục tiêu: Fog plane luôn sample đúng RT explored/vision của nhánh P1 hoặc P2.
        /// Cách hoạt động: Gán texture từ <see cref="FactionFogSystemReference"/> lên material instance của plane.
        /// </summary>
        void SyncFogPlaneMaterial()
        {
            if (fogPlaneRenderer == null || fogSystemReference == null)
            {
                return;
            }

            fogSystemReference.EnsureReferences();

            RenderTexture explored = fogSystemReference.ExploredRenderTexture;
            RenderTexture vision = fogSystemReference.VisionRenderTexture;
            if (explored == null || vision == null)
            {
                return;
            }

            Material material = fogPlaneRenderer.material;
            material.SetTexture(ExploredTextureId, explored);
            material.SetTexture(VisionTextureId, vision);
            material.SetTexture(BaseMapId, vision);
            material.SetTexture(MainTexId, vision);
        }
    }
}
