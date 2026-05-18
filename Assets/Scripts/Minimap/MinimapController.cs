using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Gắn trên Minimap Container: hiển thị RT từ MinimapRenderCamera và click di chuyển camera.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MinimapMapBoundsSO mapBounds;

        [Header("Render")]
        [SerializeField] private MinimapRenderCamera renderCamera;
        [SerializeField] private RawImage minimapDisplay;
        [SerializeField] private bool hideStaticBackground = true;
        [SerializeField] private Image backgroundImage;

        [Header("UI")]
        [SerializeField] private MinimapInputHandler inputHandler;
        [SerializeField] private MinimapUnitIconsController unitIcons;
        [SerializeField] private MinimapSupplyIconsController supplyIcons;
        [SerializeField] private MinimapIconStyleSO iconStyle;
        [SerializeField] private MinimapIconView iconPrefab;
        [SerializeField] private MinimapFogSystemReference fogSystem;
        [SerializeField] private MinimapExploredFogOverlay exploredFogOverlay;
        [SerializeField] private MonoBehaviour cameraNavigatorBehaviour;

        private void Awake()
        {
            ResolveReferences();
            WireNavigator();
            EnsureFogSystem();
            EnsureUnitIcons();
            EnsureSupplyIcons();
        }

        private void Start()
        {
            if (renderCamera != null)
            {
                renderCamera.SetMapBounds(mapBounds);
                IMinimapCameraNavigator navigator = ResolveNavigator();
                if (navigator != null)
                {
                    renderCamera.SetFollowTarget(navigator.CameraTargetTransform);
                }
            }

            RefreshMinimapDisplay();
        }

        /// <summary>
        /// Mục tiêu: Gán RT live lên RawImage sau khi MinimapRenderCamera đã tạo texture; giữ ảnh tĩnh khi chưa có RT.
        /// Cách hoạt động: Start gọi sau mọi Awake; bật Background chỉ khi không có live map hợp lệ.
        /// </summary>
        private void RefreshMinimapDisplay()
        {
            if (renderCamera == null)
            {
                renderCamera = FindFirstObjectByType<MinimapRenderCamera>();
            }

            bool hasLiveTexture = renderCamera != null && renderCamera.TargetTexture != null;

            if (backgroundImage != null)
            {
                bool showStaticMap = !hideStaticBackground || !hasLiveTexture;
                backgroundImage.enabled = showStaticMap;
            }

            if (minimapDisplay == null)
            {
                return;
            }

            if (!hasLiveTexture)
            {
                minimapDisplay.texture = null;
                return;
            }

            minimapDisplay.texture = renderCamera.TargetTexture;
            minimapDisplay.color = Color.white;
            minimapDisplay.uvRect = new Rect(0f, 0f, 1f, 1f);
            minimapDisplay.enabled = true;
        }

        private void WireNavigator()
        {
            IMinimapCameraNavigator navigator = ResolveNavigator();
            if (navigator == null)
            {
                return;
            }

            inputHandler?.SetCameraNavigator(cameraNavigatorBehaviour);
        }

        private IMinimapCameraNavigator ResolveNavigator()
        {
            if (cameraNavigatorBehaviour is not IMinimapCameraNavigator)
            {
                cameraNavigatorBehaviour = FindFirstObjectByType<PlayerInput>();
            }

            return cameraNavigatorBehaviour as IMinimapCameraNavigator;
        }

        private void ResolveReferences()
        {
            Transform background = transform.Find("Background");
            if (backgroundImage == null && background != null)
            {
                backgroundImage = background.GetComponent<Image>();
            }

            Transform mask = background != null ? background.Find("Minimap Mask") : null;
            if (mask == null)
            {
                return;
            }

            if (minimapDisplay == null)
            {
                Transform render = mask.Find("Minimap Render");
                if (render == null)
                {
                    render = mask.Find("Fog Overlay");
                }

                if (render != null)
                {
                    minimapDisplay = render.GetComponent<RawImage>();
                }
            }

            if (inputHandler == null)
            {
                inputHandler = mask.GetComponent<MinimapInputHandler>();
            }

            if (unitIcons == null)
            {
                Transform icons = mask.Find("Icons");
                if (icons != null)
                {
                    unitIcons = icons.GetComponent<MinimapUnitIconsController>();
                }
            }

            if (renderCamera == null)
            {
                renderCamera = FindFirstObjectByType<MinimapRenderCamera>();
            }
        }

        /// <summary>
        /// Mục tiêu: Bật lớp icon unit trên minimap (UI overlay, pool MinimapIconView).
        /// Cách hoạt động: Tìm child Icons, gắn MinimapUnitIconsController nếu thiếu, truyền bounds/style/prefab.
        /// </summary>
        private void EnsureUnitIcons()
        {
            if (mapBounds == null)
            {
                return;
            }

            Transform background = transform.Find("Background");
            Transform mask = background != null ? background.Find("Minimap Mask") : null;
            if (mask == null)
            {
                return;
            }

            Transform iconsTransform = mask.Find("Icons");
            if (iconsTransform == null)
            {
                return;
            }

            iconsTransform.gameObject.SetActive(true);

            if (unitIcons == null)
            {
                unitIcons = iconsTransform.GetComponent<MinimapUnitIconsController>();
            }

            if (unitIcons == null)
            {
                unitIcons = iconsTransform.gameObject.AddComponent<MinimapUnitIconsController>();
            }

            if (unitIcons != null && mapBounds != null && iconStyle != null && iconPrefab != null)
            {
                unitIcons.Configure(mapBounds, iconStyle, iconPrefab, fogSystem);
                return;
            }

            if (iconPrefab == null)
            {
                Debug.LogWarning(
                    "MinimapController: chưa gán Icon Prefab (Assets/UI/Prefabs/Minimap/MinimapIcon.prefab).",
                    this);
                return;
            }

            if (iconStyle == null)
            {
                Debug.LogWarning(
                    "MinimapController: chưa gán Icon Style (DefaultMinimapIconStyle.asset).",
                    this);
                return;
            }

            unitIcons.Configure(mapBounds, iconStyle, iconPrefab, fogSystem);
        }

        private void EnsureFogSystem()
        {
            if (fogSystem != null)
            {
                fogSystem.EnsureReferences();
                return;
            }

            fogSystem = FindFirstObjectByType<MinimapFogSystemReference>();
            if (fogSystem != null)
            {
                fogSystem.EnsureReferences();
                return;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.targetTexture == null)
                {
                    continue;
                }

                if (!camera.gameObject.name.Contains("Explored"))
                {
                    continue;
                }

                fogSystem = camera.gameObject.AddComponent<MinimapFogSystemReference>();
                fogSystem.EnsureReferences();
                return;
            }
        }

        /// <summary>
        /// Mục tiêu: Bật icon supply trên minimap (SupplySO.Icon, fallback trắng nếu null).
        /// Cách hoạt động: Cùng layer Icons với unit; lắng nghe SupplySpawn/Depleted.
        /// </summary>
        private void EnsureSupplyIcons()
        {
            if (mapBounds == null || iconStyle == null || iconPrefab == null)
            {
                return;
            }

            Transform background = transform.Find("Background");
            Transform mask = background != null ? background.Find("Minimap Mask") : null;
            Transform iconsTransform = mask != null ? mask.Find("Icons") : null;
            if (iconsTransform == null)
            {
                return;
            }

            if (supplyIcons == null)
            {
                supplyIcons = iconsTransform.GetComponent<MinimapSupplyIconsController>();
            }

            if (supplyIcons == null)
            {
                supplyIcons = iconsTransform.gameObject.AddComponent<MinimapSupplyIconsController>();
            }

            supplyIcons.Configure(mapBounds, iconStyle, iconPrefab, fogSystem);
        }
    }
}
