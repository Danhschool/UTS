using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Holder camera + RT fog cho một human owner; đăng ký <see cref="FactionFogSystemsRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionFogSystemReference : MonoBehaviour, IFogMapQuery
    {
        [SerializeField] Owner factionOwner = Owner.Player1;
        [SerializeField] Camera exploredFogCamera;
        [SerializeField] RenderTexture exploredTexture;
        [SerializeField] Camera visionFogCamera;
        [SerializeField] RenderTexture visionTexture;
        [SerializeField] float exploredThreshold = 0.1f;
        [SerializeField] float visionThreshold = 0.9f;
        [SerializeField, Min(1)] private int cacheReadIntervalFrames = 2;

        readonly FogCpuTextureMirror exploredMirror = new();
        readonly FogCpuTextureMirror visionMirror = new();
        int lastExploredReadFrame = -1;
        int lastVisionReadFrame = -1;

        public Owner FactionOwner => factionOwner;
        public Camera ExploredFogCamera => exploredFogCamera;
        public Camera VisionFogCamera => visionFogCamera;
        public RenderTexture ExploredRenderTexture => exploredTexture;
        public RenderTexture VisionRenderTexture => visionTexture;

        public void ConfigureFaction(Owner owner)
        {
            if (HumanFogVisionUtility.EmitsFogVision(owner))
            {
                factionOwner = owner;
            }
        }

        public bool IsFogReady =>
            exploredFogCamera != null
            && exploredMirror.HasCpuTexture
            && visionFogCamera != null
            && visionMirror.HasCpuTexture;

        void Awake()
        {
            EnsureReferences();
        }

        void OnEnable()
        {
            EnsureReferences();
            FactionFogSystemsRegistry.Register(this);
        }

        void OnDisable()
        {
            FactionFogSystemsRegistry.Unregister(this);
        }

        void LateUpdate()
        {
            int interval = Mathf.Max(1, cacheReadIntervalFrames);
            int frame = Time.frameCount;

            if (ShouldRefreshExploredThisFrame(frame, interval))
            {
                exploredMirror.TryRefresh(exploredTexture, 1);
                lastExploredReadFrame = frame;
            }

            if (ShouldRefreshVisionThisFrame(frame, interval))
            {
                visionMirror.TryRefresh(visionTexture, 1);
                lastVisionReadFrame = frame;
            }
        }

        bool ShouldRefreshExploredThisFrame(int frame, int interval) =>
            lastExploredReadFrame < 0 || frame - lastExploredReadFrame >= interval;

        bool ShouldRefreshVisionThisFrame(int frame, int interval) =>
            lastVisionReadFrame < 0 || frame - lastVisionReadFrame >= interval;

        public void EnsureReferences()
        {
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

        public bool IsWorldExplored(Vector3 worldPosition) =>
            exploredMirror.SampleWorldVisible(
                exploredFogCamera,
                worldPosition,
                exploredThreshold,
                requireInsideUv: true);

        public bool IsWorldVisible(Vector3 worldPosition) =>
            visionMirror.SampleWorldVisible(
                visionFogCamera,
                worldPosition,
                visionThreshold,
                requireInsideUv: true);

        void OnDestroy()
        {
            exploredMirror.Release();
            visionMirror.Release();
        }
    }
}
