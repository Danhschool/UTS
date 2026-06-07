using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
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
        [SerializeField] private bool enableExploredFogOverlay = false;
        [SerializeField] private Owner fogFactionOwner = Owner.Player1;
        [SerializeField] private MonoBehaviour cameraNavigatorBehaviour;

        public MinimapFogSystemReference FogSystemReference => fogSystem;
        public Owner FogFactionOwner => fogFactionOwner;

        /// <summary>
        /// Mục tiêu: MP — HUD P2 minimap sample RT fog Player2 thay vì chờ LocalHumanOwnerService.
        /// Cách hoạt động: Gán owner, refresh overlay + icon filter cùng phe.
        /// </summary>
        public void BindFactionOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            fogFactionOwner = owner;
            unitIcons?.BindLocalOwner(owner);
            RefreshFogPresentation();
        }

        /// <summary>
        /// Mục tiêu: Sau MP bind faction fog — overlay + icon dùng đúng RT P1/P2.
        /// Cách hoạt động: EnsureFogSystem + Configure lại MinimapExploredFogOverlay.
        /// </summary>
        public void RefreshFogPresentation()
        {
            EnsureFogSystem();
        }

        /// <summary>
        /// Mục tiêu: MP — chỉ HUD active bật Fog Overlay; rig P1 tắt trên client P2.
        /// </summary>
        public void SetExploredFogOverlayEnabled(bool enabled)
        {
            enableExploredFogOverlay = enabled;
            WireExploredFogOverlay();
        }

        private void Awake()
        {
            ResolveReferences();
            WireNavigator();
            EnsureFogSystem();
            EnsureUnitIcons();
            EnsureSupplyIcons();
        }

        private void OnEnable()
        {
            if (enableExploredFogOverlay)
            {
                WireExploredFogOverlay();
            }
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

            EnsureFogSystem();
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
                minimapDisplay = FindMinimapRenderRawImage(mask);
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
            fogSystem ??= GetComponent<MinimapFogSystemReference>();
            fogSystem ??= GetComponentInChildren<MinimapFogSystemReference>(true);

            if (fogSystem == null)
            {
                fogSystem = gameObject.AddComponent<MinimapFogSystemReference>();
            }

            fogSystem.EnsureReferences();
            WireExploredFogOverlay();
        }

        /// <summary>
        /// Mục tiêu: Lớp fog minimap (đen/mờ/trong) dùng cùng RT với icon filter — scene MP thường thiếu object này.
        /// Cách hoạt động: Tìm hoặc tạo Fog Overlay trên Minimap content; Configure(bounds, fogSystem).
        /// </summary>
        private void WireExploredFogOverlay()
        {
            if (exploredFogOverlay == null)
            {
                exploredFogOverlay = GetComponentInChildren<MinimapExploredFogOverlay>(true);
            }

            if (!enableExploredFogOverlay)
            {
                if (exploredFogOverlay != null)
                {
                    exploredFogOverlay.gameObject.SetActive(false);
                }

                return;
            }

            if (exploredFogOverlay == null)
            {
                exploredFogOverlay = CreateExploredFogOverlay();
            }

            if (exploredFogOverlay == null || mapBounds == null || fogSystem == null)
            {
                return;
            }

            exploredFogOverlay.Configure(mapBounds, fogSystem, fogFactionOwner);
            exploredFogOverlay.gameObject.SetActive(true);
        }

        /// <summary>
        /// Mục tiêu: RtsNet_Game không có child Fog Overlay — tạo RawImage + shader overlay trên Minimap Render.
        /// Cách hoạt động: Stretch full rect trên content root; sibling index ngay sau Minimap Render.
        /// </summary>
        MinimapExploredFogOverlay CreateExploredFogOverlay()
        {
            Transform mask = FindMinimapMask();
            if (mask == null)
            {
                return null;
            }

            Transform contentRoot = mask.Find("Minimap") ?? mask;
            Transform existing = contentRoot.Find("Fog Overlay");
            if (existing != null)
            {
                return existing.GetComponent<MinimapExploredFogOverlay>();
            }

            GameObject overlayObject = new GameObject(
                "Fog Overlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(MinimapExploredFogOverlay));
            overlayObject.layer = contentRoot.gameObject.layer;

            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(contentRoot, false);
            StretchRectToParent(overlayRect);

            RawImage rawImage = overlayObject.GetComponent<RawImage>();
            rawImage.raycastTarget = false;

            Transform renderTransform = contentRoot.Find("Minimap Render");
            if (renderTransform != null)
            {
                overlayObject.transform.SetSiblingIndex(renderTransform.GetSiblingIndex() + 1);
            }

            return overlayObject.GetComponent<MinimapExploredFogOverlay>();
        }

        static void StretchRectToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        static RawImage FindMinimapRenderRawImage(Transform mask)
        {
            Transform render = mask.Find("Minimap Render");
            if (render == null)
            {
                Transform minimap = mask.Find("Minimap");
                if (minimap != null)
                {
                    render = minimap.Find("Minimap Render");
                }
            }

            if (render == null)
            {
                render = mask.Find("Fog Overlay");
            }

            return render != null ? render.GetComponent<RawImage>() : null;
        }

        Transform FindMinimapMask()
        {
            Transform background = transform.Find("Background");
            if (background == null)
            {
                return null;
            }

            return background.Find("Minimap Mask");
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
