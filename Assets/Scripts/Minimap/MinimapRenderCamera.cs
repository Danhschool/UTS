using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Camera ortho nhìn từ trên, render scene vào RT hiển thị trên minimap UI.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapRenderCamera : MonoBehaviour
    {
        [Header("Output")]
        [SerializeField] private RenderTexture targetTexture;
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 512;

        [Header("Camera")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private float cameraHeight = 150f;
        [SerializeField] private float orthographicSize = 90f;
        [SerializeField] private LayerMask cullingMask = ~0;

        [Header("Fog of war (minimap)")]
        [Tooltip("Vẽ Fog of War Plane để vùng chưa khám phá bị che trên minimap.")]
        [SerializeField] private bool includeFogOfWarPlane = false;
        [Tooltip("Không render layer chỉ dùng cho camera vision RT.")]
        [SerializeField] private bool excludeFogVisionLayer = true;
        [SerializeField] private bool excludeUiLayer = true;
        [SerializeField] private int fogOfWarPlaneLayer = 13;
        [SerializeField] private int fogVisionLayer = OwnerFogVisionLayers.DefaultPlayer1VisionLayer;
        [SerializeField] private int fogVisionLayerPlayer2 = OwnerFogVisionLayers.DefaultPlayer2VisionLayer;
        [SerializeField] private bool excludeFogVisionLayerPlayer2 = true;
        [SerializeField] private int uiLayer = 5;

        [Header("Follow")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private bool followGameplayCameraTarget = true;

        [Header("Bounds")]
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private bool syncMapBounds = true;

        public RenderTexture TargetTexture => targetTexture;
        public Camera MinimapCamera => minimapCamera;

        private void Awake()
        {
            ResolveFogVisionLayersFromProject();
            EnsureRenderTexture();
            EnsureCamera();
            ApplyCameraSettings();
            SyncBounds();
        }

        /// <summary>
        /// Mục tiêu: Minimap không vẽ layer vision RT (14 + Fog Vision Player2/15 nếu có trong TagManager).
        /// Cách hoạt động: Đọc <see cref="OwnerFogVisionLayers"/> — human P1/P2 cùng 14; minimap loại trừ thêm 15.
        /// </summary>
        private void ResolveFogVisionLayersFromProject()
        {
            int player1Layer = OwnerFogVisionLayers.GetLayer(Owner.Player1);
            if (player1Layer >= 0)
            {
                fogVisionLayer = player1Layer;
            }

            int dedicatedP2Vision = OwnerFogVisionLayers.GetDedicatedPlayer2VisionLayerIndex();
            fogVisionLayerPlayer2 = dedicatedP2Vision >= 0
                ? dedicatedP2Vision
                : OwnerFogVisionLayers.GetLayer(Owner.Player2);
        }

        private void LateUpdate()
        {
            UpdateFollowPosition();
            if (syncMapBounds)
            {
                SyncBounds();
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            UpdateFollowPosition();
        }

        public void SetMapBounds(MinimapMapBoundsSO bounds)
        {
            mapBounds = bounds;
            SyncBounds();
        }

        private void EnsureRenderTexture()
        {
            if (targetTexture != null)
            {
                return;
            }

            targetTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "Minimap Render Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            targetTexture.Create();
        }

        /// <summary>
        /// Mục tiêu: Tạo camera con nhìn thẳng xuống nếu chưa gán trong Inspector.
        /// Cách hoạt động: Ortho, targetTexture, không render lên Game view chính.
        /// </summary>
        private void EnsureCamera()
        {
            if (minimapCamera != null)
            {
                return;
            }

            GameObject cameraObject = new("Minimap Render Camera");
            cameraObject.transform.SetParent(transform, false);
            minimapCamera = cameraObject.AddComponent<Camera>();
        }

        private void ApplyCameraSettings()
        {
            if (minimapCamera == null)
            {
                return;
            }

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = orthographicSize;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = Color.black;
            minimapCamera.cullingMask = BuildMinimapCullingMask();
            minimapCamera.targetTexture = targetTexture;
            minimapCamera.depth = -10f;
            minimapCamera.nearClipPlane = 0.3f;
            minimapCamera.farClipPlane = 500f;
            minimapCamera.useOcclusionCulling = false;
        }

        /// <summary>
        /// Mục tiêu: Camera minimap luôn ở trên target gameplay (chỉ XZ).
        /// Cách hoạt động: Giữ Y = cameraHeight; xoay cố định nhìn xuống.
        /// </summary>
        private void UpdateFollowPosition()
        {
            if (followGameplayCameraTarget && followTarget == null)
            {
                PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
                if (playerInput != null)
                {
                    followTarget = playerInput.CameraTargetTransform;
                }
            }

            if (followTarget == null)
            {
                transform.position = new Vector3(transform.position.x, cameraHeight, transform.position.z);
                if (minimapCamera != null)
                {
                    minimapCamera.transform.position = transform.position;
                }

                return;
            }

            Vector3 position = followTarget.position;
            position.y = cameraHeight;
            transform.position = position;
            if (minimapCamera != null)
            {
                minimapCamera.transform.position = position;
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>
        /// Mục tiêu: Minimap thấy terrain + unit nhưng che vùng chưa explore bằng fog plane, không vẽ vision RT layer.
        /// Cách hoạt động: OR layer Fog of War (13), AND NOT layer Fog of War Vision (14) và UI.
        /// </summary>
        private int BuildMinimapCullingMask()
        {
            int mask = cullingMask.value;

            if (includeFogOfWarPlane && fogOfWarPlaneLayer >= 0 && fogOfWarPlaneLayer < 32)
            {
                mask |= 1 << fogOfWarPlaneLayer;
            }

            if (excludeFogVisionLayer && fogVisionLayer >= 0 && fogVisionLayer < 32)
            {
                mask &= ~(1 << fogVisionLayer);
            }

            if (excludeFogVisionLayerPlayer2 && fogVisionLayerPlayer2 >= 0 && fogVisionLayerPlayer2 < 32)
            {
                mask &= ~(1 << fogVisionLayerPlayer2);
            }

            if (excludeUiLayer && uiLayer >= 0 && uiLayer < 32)
            {
                mask &= ~(1 << uiLayer);
            }

            return mask;
        }

        private void SyncBounds()
        {
            if (mapBounds == null || minimapCamera == null)
            {
                return;
            }

            mapBounds.SyncFromOrthographicCamera(minimapCamera, targetTexture);
        }

        private void OnDestroy()
        {
            if (targetTexture == null || targetTexture.name != "Minimap Render Texture")
            {
                return;
            }

            if (targetTexture.IsCreated())
            {
                targetTexture.Release();
            }

            Destroy(targetTexture);
        }
    }
}
