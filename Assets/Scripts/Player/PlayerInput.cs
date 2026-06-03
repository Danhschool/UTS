using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Hotkeys.Targets;
using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Netplay;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    public class PlayerInput : MonoBehaviour,
        IMinimapCameraNavigator,
        IHotkeyUnitTypeSelectTarget,
        IHotkeyActionBarTarget
    {
        [SerializeField] private Rigidbody cameraTarget;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private new Camera camera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private LayerMask selectableUnitsLayers;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private LayerMask floorLayers;
        [SerializeField] private RectTransform selectionBox;
        [Tooltip("Ghost đặt nhà: khi vị trí không hợp lệ, mỗi slot material trên renderer được thay bằng material này (đỏ). Để trống thì dùng MaterialPropertyBlock _BaseColor/_Color.")]
        [SerializeField] private Material ghostPlacementInvalidMaterial;
        [Tooltip("Bán kính > 0: OverlapSphere bổ sung — nếu chạm collider có NavMeshObstacle (bật) thì coi là không đặt được. 0 = chỉ dùng Restrictions trên lệnh.")]
        [SerializeField] private float placementNavMeshObstacleProbeRadius;
        [SerializeField] private LayerMask placementNavMeshObstacleProbeLayers = ~0;

        private Vector2 startingMousePosition;

        private BaseCommand activeCommand;
        private GameObject ghostInstance;
        private Renderer[] ghostPlacementRenderers;
        private Material[][] ghostPlacementOriginalSharedMaterials;
        private bool ghostPlacementVisualCached;
        private bool lastGhostPlacementValid = true;
        private MaterialPropertyBlock ghostPlacementMpb;
        private static readonly int ShaderIdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ShaderIdColor = Shader.PropertyToID("_Color");
        private bool placementGhostPinnedToWorld;
        private Vector3 placementGhostPinnedPosition;
        private BaseCommand pinnedPlacementRestrictionsCommand;
        private bool wasMouseDownOnUI;
        private CinemachineFollow cinemachineFollow;
        private Vector3 startingFollowOffset;
        private float maxRotationAmount;
        private float scrollZoomScale = 1f;
        private float smoothedZoomScale = 1f;
        private float zoomTargetScale = 1f;
        private float zoomScaleVelocity;
        private float smoothedRotX;
        private float rotTargetX;
        private float rotXVelocity;
        private float scrollOrthoSize;
        private float initialScrollOrthoSize;
        private float orthoSizeVelocity;
        private float smoothedFollowZ;
        private float followZVelocity;
        private Owner localOwner = Owner.Player1;

        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<BaseBuilding> aliveBuildings = new(32);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

        private const float UnitDoubleClickIntervalSeconds = 0.35f;
        private const float DragSelectCancelDoubleClickSqrPixels = 4f;
        private float lastUnitLeftClickTime = -1f;
        private AbstractUnit lastUnitLeftClickTarget;
        private GameObject unitTypeCyclePrefab;
        private int unitTypeCycleIndex = -1;

        private void Awake()
        {
            if (!cinemachineCamera.TryGetComponent(out cinemachineFollow))
            {
                Debug.LogError("Cinemachine Camera did not have CinemachineFollow. Zoom functionality will not work!");
            }

            startingFollowOffset = cinemachineFollow.FollowOffset;
            maxRotationAmount = Mathf.Abs(cinemachineFollow.FollowOffset.z);
            smoothedRotX = cinemachineFollow.FollowOffset.x;
            smoothedFollowZ = cinemachineFollow.FollowOffset.z;
            scrollOrthoSize = camera != null ? camera.orthographicSize : 20f;
            initialScrollOrthoSize = scrollOrthoSize;
            scrollZoomScale = smoothedZoomScale = zoomTargetScale = ComputeInitialZoomScaleFromFollowOffset();

            localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            SubscribeBus(localOwner);
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;

            if (GetComponent<PlayerInputHotkeyIntegration>() == null)
            {
                gameObject.AddComponent<PlayerInputHotkeyIntegration>();
            }
        }

        void Start()
        {
            RegisterExistingLocalCommandables();
            PurgeNonLocalFromSelection();
        }

        private void OnDestroy()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            UnsubscribeBus(localOwner);
            DisposePlacementGhost();
        }

        void OnLocalOwnerChanged(Owner owner)
        {
            UnsubscribeBus(localOwner);
            localOwner = owner;
            aliveUnits.RemoveWhere(unit => unit == null || unit.Owner != localOwner);
            aliveBuildings.RemoveWhere(building => building == null || building.Owner != localOwner);
            selectedUnits.Clear();
            SubscribeBus(localOwner);
            RegisterExistingLocalCommandables();
        }

        void SubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[owner] += HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[owner] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleBuildingDeath;
            Bus<BuildingConstructStartedEvent>.OnEvent[owner] += OnBuildingConstructStarted;
        }

        void UnsubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[owner] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[owner] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleBuildingDeath;
            Bus<BuildingConstructStartedEvent>.OnEvent[owner] -= OnBuildingConstructStarted;
        }

        private void OnBuildingConstructStarted(BuildingConstructStartedEvent evt)
        {
            if (evt.Owner != localOwner)
            {
                return;
            }

            if (placementGhostPinnedToWorld)
            {
                DisposePlacementGhost();
            }
        }

        /// <summary>
        /// Mục tiêu: Gỡ ghost đặt nhà (ghim hoặc theo chuột) và reset trạng thái pin.
        /// Cách hoạt động: Hủy pin, xóa lệnh cache cho Restrictions, Destroy ghost và cache material.
        /// </summary>
        private void DisposePlacementGhost()
        {
            placementGhostPinnedToWorld = false;
            pinnedPlacementRestrictionsCommand = null;
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                ClearGhostPlacementVisualCache();
            }
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (!IsLocalOwnedSelectable(evt.Unit))
            {
                return;
            }

            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit != null && evt.Unit.Owner == ResolveLocalOwner())
            {
                aliveUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            Owner local = ResolveLocalOwner();
            if (evt.Building != null
                && evt.Building.Owner == local
                && evt.Owner == evt.Building.Owner)
            {
                aliveBuildings.Add(evt.Building);
            }
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building != null)
            {
                aliveBuildings.Remove(evt.Building);
                selectedUnits.Remove(evt.Building);
            }
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            DisposePlacementGhost();
            SetActiveCommand(evt.Command);
            if (!activeCommand.RequiresClickToActivate)
            {
                ActivateAction(new RaycastHit());
            }
            else if (activeCommand.GhostPrefab != null)
            {
                ghostInstance = Instantiate(activeCommand.GhostPrefab);
                CacheGhostPlacementVisuals();
            }
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ lệnh chờ với UI action bar (nút trắng khi đang chọn lệnh).
        /// Cách hoạt động: Gán activeCommand và raise <see cref="ActiveCommandChangedEvent"/> theo localOwner.
        /// </summary>
        void SetActiveCommand(BaseCommand command)
        {
            activeCommand = command;
            Bus<ActiveCommandChangedEvent>.Raise(ResolveLocalOwner(), new ActiveCommandChangedEvent(command));
        }

        private void Update()
        {
            if (!GameplayStartupGate.IsGameplayUnlocked
                && GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            HandlePanning();
            HandleScrollZoomInput();
            zoomTargetScale = Keyboard.current.endKey.isPressed
                ? cameraConfig.MinZoomScale
                : scrollZoomScale;

            float zoomSmoothSeconds = Mathf.Max(0.01f, 0.35f / Mathf.Max(0.01f, cameraConfig.ZoomSpeed));
            smoothedZoomScale = Mathf.SmoothDamp(
                smoothedZoomScale,
                zoomTargetScale,
                ref zoomScaleVelocity,
                zoomSmoothSeconds);

            UpdateRotationTargetX();
            float rotSmoothSeconds = Mathf.Max(0.01f, 0.35f / Mathf.Max(0.01f, cameraConfig.RotationSpeed));
            smoothedRotX = Mathf.SmoothDamp(smoothedRotX, rotTargetX, ref rotXVelocity, rotSmoothSeconds);

            float zTarget = GetFollowOffsetZTarget();
            smoothedFollowZ = Mathf.SmoothDamp(smoothedFollowZ, zTarget, ref followZVelocity, rotSmoothSeconds);

            ApplyCinemachineFollowOffset();

            HandleGhost();
            HandleRightClick();
            HandleDragSelect();
        }

        private void FixedUpdate()
        {
            ClampCameraTargetToPanLimits();
        }

        /// <summary>
        /// Khôi phục tỉ lệ zoom ban đầu từ offset Cinemachine (mặt phẳng YZ) để không giật khi vào Play.
        /// </summary>
        /// <remarks>
        /// So sánh độ dài vector (Y,Z) hiện tại với offset gốc; clamp theo Min/Max trong <see cref="CameraConfig"/>.
        /// </remarks>
        private float ComputeInitialZoomScaleFromFollowOffset()
        {
            Vector2 startYZ = new(startingFollowOffset.y, startingFollowOffset.z);
            Vector2 curYZ = new(cinemachineFollow.FollowOffset.y, cinemachineFollow.FollowOffset.z);
            float denom = startYZ.magnitude;
            if (denom < 1e-4f)
            {
                return 1f;
            }

            float ratio = curYZ.magnitude / denom;
            return Mathf.Clamp(ratio, cameraConfig.MinZoomScale, cameraConfig.MaxZoomScale);
        }

        /// <summary>
        /// Giữ điểm neo camera (Rigidbody) trong vùng bản đồ cho phép trên trục XZ.
        /// </summary>
        /// <remarks>
        /// Gọi trong FixedUpdate để khớp vật lý; kết hợp với chặn vận tốc pan trong <see cref="HandlePanning"/> để tránh rung mép.
        /// </remarks>
        private void ClampCameraTargetToPanLimits()
        {
            if (cameraTarget == null || !cameraConfig.EnablePanLimits)
            {
                return;
            }

            Vector3 p = cameraTarget.position;
            p.x = Mathf.Clamp(p.x, cameraConfig.PanLimitMinXZ.x, cameraConfig.PanLimitMaxXZ.x);
            p.z = Mathf.Clamp(p.z, cameraConfig.PanLimitMinXZ.y, cameraConfig.PanLimitMaxXZ.y);
            cameraTarget.MovePosition(p);
        }

        /// <summary>
        /// Cập nhật offset Cinemachine: perspective thì scale Y+Z (dolly theo hướng mặc định); orthographic thì chỉnh orthographicSize.
        /// </summary>
        /// <remarks>
        /// Không đụng tới Field of View — zoom giống phóng to / lùi camera dọc hướng offset thay vì đổi góc mở ống kính.
        /// </remarks>
        private void ApplyCinemachineFollowOffset()
        {
            if (camera != null && camera.orthographic)
            {
                float orthoTarget = Keyboard.current.endKey.isPressed
                    ? cameraConfig.MinOrthographicSize
                    : scrollOrthoSize;
                float oSmooth = Mathf.Max(0.01f, 0.35f / Mathf.Max(0.01f, cameraConfig.ZoomSpeed));
                camera.orthographicSize = Mathf.SmoothDamp(
                    camera.orthographicSize,
                    orthoTarget,
                    ref orthoSizeVelocity,
                    oSmooth);
                cinemachineFollow.FollowOffset = new Vector3(
                    smoothedRotX,
                    startingFollowOffset.y,
                    smoothedFollowZ);
            }
            else
            {
                cinemachineFollow.FollowOffset = new Vector3(
                    smoothedRotX,
                    startingFollowOffset.y * smoothedZoomScale,
                    smoothedFollowZ);
            }
        }

        /// <summary>
        /// Trả về offset Z mục tiêu: khi xoay (Page Up/Down) về 0 như setup cũ; khi zoom perspective thì scale theo tỉ lệ.
        /// </summary>
        /// <remarks>
        /// Tách riêng Z khỏi X/Y để giữ hành vi tilt cũ mà vẫn kết hợp zoom dolly trên mặt phẳng YZ.
        /// </remarks>
        private float GetFollowOffsetZTarget()
        {
            if (Keyboard.current.pageDownKey.isPressed || Keyboard.current.pageUpKey.isPressed)
            {
                return 0f;
            }

            if (camera != null && camera.orthographic)
            {
                return startingFollowOffset.z;
            }

            return startingFollowOffset.z * smoothedZoomScale;
        }

        private void HandleScrollZoomInput()
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) <= 0.001f || camera == null)
            {
                return;
            }

            if (camera.orthographic)
            {
                scrollOrthoSize -= scrollY * cameraConfig.ScrollZoomSensitivity * 0.08f;
                scrollOrthoSize = Mathf.Clamp(
                    scrollOrthoSize,
                    cameraConfig.MinOrthographicSize,
                    cameraConfig.MaxOrthographicSize);
            }
            else
            {
                scrollZoomScale -= scrollY * cameraConfig.ScrollZoomSensitivity * 0.018f;
                scrollZoomScale = Mathf.Clamp(
                    scrollZoomScale,
                    cameraConfig.MinZoomScale,
                    cameraConfig.MaxZoomScale);
            }
        }

        private void UpdateRotationTargetX()
        {
            if (Keyboard.current.pageDownKey.isPressed)
            {
                rotTargetX = maxRotationAmount;
            }
            else if (Keyboard.current.pageUpKey.isPressed)
            {
                rotTargetX = -maxRotationAmount;
            }
            else
            {
                rotTargetX = startingFollowOffset.x;
            }
        }

        private void HandleGhost()
        {
            if (ghostInstance == null) return;

            if (placementGhostPinnedToWorld)
            {
                ghostInstance.transform.position = placementGhostPinnedPosition;
                bool pinnedValid = EvaluateGhostPlacementValid(placementGhostPinnedPosition, pinnedPlacementRestrictionsCommand);
                UpdateGhostPlacementVisual(pinnedValid);
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                ghostInstance.transform.position = hit.point;
                bool valid = EvaluateGhostPlacementValid(hit.point);
                UpdateGhostPlacementVisual(valid);
            }
            else
            {
                UpdateGhostPlacementVisual(false);
            }
        }

        /// <summary>
        /// Mục tiêu: Lưu sharedMaterials gốc của ghost để bật/tắt trạng thái đỏ mà không hỏng asset prefab.
        /// Cách hoạt động: Lấy mọi Renderer con (kể cả inactive), copy mảng tham chiếu material vào buffer nội bộ.
        /// </summary>
        private void CacheGhostPlacementVisuals()
        {
            ClearGhostPlacementVisualCache();
            if (ghostInstance == null)
            {
                return;
            }

            ghostPlacementRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);
            if (ghostPlacementRenderers.Length == 0)
            {
                ghostPlacementOriginalSharedMaterials = System.Array.Empty<Material[]>();
                ghostPlacementVisualCached = false;
                return;
            }

            ghostPlacementOriginalSharedMaterials = new Material[ghostPlacementRenderers.Length][];
            for (int i = 0; i < ghostPlacementRenderers.Length; i++)
            {
                Material[] shared = ghostPlacementRenderers[i].sharedMaterials;
                ghostPlacementOriginalSharedMaterials[i] = new Material[shared.Length];
                System.Array.Copy(shared, ghostPlacementOriginalSharedMaterials[i], shared.Length);
            }

            ghostPlacementVisualCached = true;
            lastGhostPlacementValid = true;
        }

        /// <summary>
        /// Mục tiêu: Bỏ cache ghost khi hủy instance để tránh tham chiếu tới object đã Destroy.
        /// Cách hoạt động: Xóa mảng và reset cờ; gọi khi Destroy ghost hoặc trước khi spawn ghost mới.
        /// </summary>
        private void ClearGhostPlacementVisualCache()
        {
            ghostPlacementRenderers = null;
            ghostPlacementOriginalSharedMaterials = null;
            ghostPlacementVisualCached = false;
            lastGhostPlacementValid = true;
        }

        /// <summary>
        /// Mục tiêu: Quyết định điểm đặt có hợp lệ không — khớp logic placement của lệnh (Restrictions) và tùy chọn NavMeshObstacle.
        /// Cách hoạt động: Gọi <see cref="BaseCommand.AllRestrictionsPass"/>; nếu bật probe thì OverlapSphere và tìm <see cref="NavMeshObstacle"/> bật trên collider/object cha.
        /// </summary>
        private bool EvaluateGhostPlacementValid(Vector3 worldPoint, BaseCommand commandForRestrictions = null)
        {
            BaseCommand cmd = commandForRestrictions != null ? commandForRestrictions : activeCommand;
            return cmd != null && cmd.AllRestrictionsPass(worldPoint);
        }

        /// <summary>
        /// Mục tiêu: Hiển thị ghost đỏ khi không đặt được, trả về material/MPB gốc khi hợp lệ.
        /// Cách hoạt động: Nếu gán <see cref="ghostPlacementInvalidMaterial"/> thì thay <c>sharedMaterials</c> từng slot; không thì tint đỏ qua MaterialPropertyBlock.
        /// </summary>
        private void UpdateGhostPlacementVisual(bool valid)
        {
            if (!ghostPlacementVisualCached || ghostPlacementRenderers == null || ghostPlacementRenderers.Length == 0)
            {
                return;
            }

            if (valid == lastGhostPlacementValid)
            {
                return;
            }

            lastGhostPlacementValid = valid;
            if (valid)
            {
                if (ghostPlacementInvalidMaterial != null)
                {
                    for (int i = 0; i < ghostPlacementRenderers.Length; i++)
                    {
                        ghostPlacementRenderers[i].sharedMaterials = ghostPlacementOriginalSharedMaterials[i];
                    }
                }
                else
                {
                    MaterialPropertyBlock empty = new();
                    for (int i = 0; i < ghostPlacementRenderers.Length; i++)
                    {
                        ghostPlacementRenderers[i].SetPropertyBlock(empty);
                    }
                }

                return;
            }

            if (ghostPlacementInvalidMaterial != null)
            {
                for (int i = 0; i < ghostPlacementRenderers.Length; i++)
                {
                    int n = ghostPlacementOriginalSharedMaterials[i].Length;
                    Material[] redSlots = new Material[n];
                    for (int j = 0; j < n; j++)
                    {
                        redSlots[j] = ghostPlacementInvalidMaterial;
                    }

                    ghostPlacementRenderers[i].sharedMaterials = redSlots;
                }
            }
            else
            {
                ghostPlacementMpb ??= new MaterialPropertyBlock();
                Color red = Color.red;
                for (int i = 0; i < ghostPlacementRenderers.Length; i++)
                {
                    Renderer r = ghostPlacementRenderers[i];
                    r.GetPropertyBlock(ghostPlacementMpb);
                    ghostPlacementMpb.SetColor(ShaderIdBaseColor, red);
                    ghostPlacementMpb.SetColor(ShaderIdColor, red);
                    r.SetPropertyBlock(ghostPlacementMpb);
                }
            }
        }

        private void HandleDragSelect()
        {
            if (selectionBox == null) { return; }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDown();
            }
            else if (Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDrag();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                HandleMouseUp();
            }
        }

        private void HandleMouseUp()
        {
            if (!wasMouseDownOnUI && activeCommand == null && !Keyboard.current.shiftKey.isPressed)
            {
                DeselectAllUnits();
            }

            // Khung kéo đã có unit → chỉ chọn unit, không raycast building/commandable dưới con trỏ.
            if (addedUnits.Count == 0)
            {
                HandleLeftClick();
            }

            foreach (AbstractUnit unit in addedUnits)
            {
                TrySelectLocalOwned(unit);
            }
            selectionBox.gameObject.SetActive(false);
        }

        private void HandleMouseDrag()
        {
            if (activeCommand != null || wasMouseDownOnUI) return;

            if (selectionBox.sizeDelta.sqrMagnitude > DragSelectCancelDoubleClickSqrPixels)
            {
                ResetUnitDoubleClickTracking();
            }

            Bounds selectionBoxBounds = ResizeSelectionBox();
            foreach (AbstractUnit unit in aliveUnits.Where(aliveUnits => aliveUnits.gameObject.activeInHierarchy))
            {
                if (!IsOwnedByLocalPlayer(unit))
                {
                    continue;
                }

                Vector2 unitPosition = camera.WorldToScreenPoint(unit.transform.position);

                if (selectionBoxBounds.Contains(unitPosition))
                {
                    addedUnits.Add(unit);
                }
            }
        }

        private void HandleMouseDown()
        {
            selectionBox.sizeDelta = Vector2.zero;
            selectionBox.gameObject.SetActive(true);
            startingMousePosition = Mouse.current.position.ReadValue();
            addedUnits.Clear();
            wasMouseDownOnUI = EventSystem.current.IsPointerOverGameObject();
        }

        private void DeselectAllUnits()
        {
            ISelectable[] currentlySelectedUnits = selectedUnits.ToArray();
            foreach(ISelectable selectable in currentlySelectedUnits)
            {
                selectable.Deselect();
            }
        }

        /// <summary>
        /// Mục tiêu: Xử lý Esc từ hệ thống hotkey (hủy đặt công trình / bỏ chọn).
        /// Cách hoạt động: Ưu tiên hủy ghost và active command; nếu không có thì bỏ chọn toàn bộ unit.
        /// </summary>
        public void CancelFromHotkey()
        {
            EnsureLocalOwnerBeforeHotkey();

            if (activeCommand != null || ghostInstance != null)
            {
                DisposePlacementGhost();
                SetActiveCommand(null);
                return;
            }

            DeselectAllUnits();
        }

        /// <summary>
        /// Mục tiêu: Hotkey Q/W/E/R hoặc A/S/D — chọn unit hoặc nhà theo prefab archetype trên màn hình.
        /// Cách hoạt động: Prefab nhà → luồng building; còn lại → unit (một hoặc tất cả khi Ctrl).
        /// </summary>
        public void OnHotkeySelectUnitType(GameObject referencePrefab, bool selectAllOnScreen)
        {
            EnsureLocalOwnerBeforeHotkey();

            if (referencePrefab == null)
            {
                return;
            }

            if (BuildingKindMatching.IsBuildingPrefab(referencePrefab))
            {
                if (selectAllOnScreen)
                {
                    SelectAllBuildingsOfKindOnScreen(referencePrefab);
                }
                else
                {
                    SelectOneBuildingOfKindOnScreen(referencePrefab);
                }

                return;
            }

            if (selectAllOnScreen)
            {
                SelectAllUnitsOfKindOnScreen(referencePrefab);
                return;
            }

            SelectOneUnitOfKindOnScreen(referencePrefab);
        }

        /// <summary>
        /// Mục tiêu: Phím 1–9 kích hoạt lệnh slot trên thanh action (cùng thứ tự UI).
        /// Cách hoạt động: Resolve lệnh theo selection → validate supply/lock → raise CommandSelectedEvent.
        /// </summary>
        public void OnHotkeyActionBarSlot(int slotIndex)
        {
            EnsureLocalOwnerBeforeHotkey();

            List<AbstractCommandable> commandables = CollectSelectedCommandables();
            if (commandables.Count == 0)
            {
                return;
            }

            if (!ActionBarCommandResolver.TryGetCommandForSlot(commandables, slotIndex, out BaseCommand command))
            {
                return;
            }

            AbstractCommandable[] units = commandables.ToArray();
            if (!ActionBarCommandExecution.TryValidateForExecution(command, units))
            {
                return;
            }

            Bus<CommandSelectedEvent>.Raise(ResolveLocalOwner(), new CommandSelectedEvent(command));
        }

        /// <summary>
        /// Mục tiêu: Home — reset zoom và góc nghiêng camera về mặc định scene.
        /// Cách hoạt động: Khôi phục offset Cinemachine và orthographic size ban đầu.
        /// </summary>
        public void ResetCameraFromHotkey()
        {
            if (cinemachineFollow == null)
            {
                return;
            }

            scrollZoomScale = 1f;
            zoomTargetScale = 1f;
            smoothedZoomScale = 1f;
            zoomScaleVelocity = 0f;
            rotTargetX = startingFollowOffset.x;
            smoothedRotX = rotTargetX;
            rotXVelocity = 0f;
            smoothedFollowZ = startingFollowOffset.z;
            followZVelocity = 0f;

            if (camera != null && camera.orthographic)
            {
                scrollOrthoSize = initialScrollOrthoSize;
                orthoSizeVelocity = 0f;
            }

            ApplyCinemachineFollowOffset();
        }

        /// <summary>
        /// Mục tiêu: F — pan camera tới unit/nhà phe local đang được chọn.
        /// Cách hoạt động: Lấy commandable local đầu tiên trong selection → PanCameraToWorldPosition.
        /// </summary>
        public void FollowSelectedFromHotkey()
        {
            EnsureLocalOwnerBeforeHotkey();

            List<AbstractCommandable> commandables = CollectSelectedCommandables();
            if (commandables.Count == 0)
            {
                return;
            }

            PanCameraToWorldPosition(commandables[0].transform.position);
        }

        /// <summary>
        /// Mục tiêu: Delete / Shift+Delete — xóa selection phe local (debug / editor).
        /// Cách hoạt động: Purge selection địch → gọi Die() trên từng commandable local; immediate bỏ qua hiệu ứng chờ.
        /// </summary>
        public void DeleteSelectionFromHotkey(bool immediate)
        {
            EnsureLocalOwnerBeforeHotkey();

            ISelectable[] snapshot = selectedUnits.ToArray();
            DeselectAllUnits();

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is not AbstractCommandable commandable || !IsOwnedByLocalPlayer(commandable))
                {
                    continue;
                }

                if (immediate)
                {
                    Destroy(commandable.gameObject);
                }
                else
                {
                    commandable.Die();
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Chọn một unit cùng loại trên màn hình; bấm lại cùng phím sẽ chuyển sang unit kế tiếp.
        /// Cách hoạt động: Thu thập danh sách khớp prefab → DeselectAll → cycle index modulo count → Select.
        /// </summary>
        public void SelectOneUnitOfKindOnScreen(GameObject referencePrefab)
        {
            List<AbstractUnit> matches = CollectUnitsOfKindOnScreen(referencePrefab);
            if (matches.Count == 0)
            {
                return;
            }

            DeselectAllUnits();

            if (unitTypeCyclePrefab != referencePrefab)
            {
                unitTypeCyclePrefab = referencePrefab;
                unitTypeCycleIndex = -1;
            }

            unitTypeCycleIndex = (unitTypeCycleIndex + 1) % matches.Count;
            TrySelectLocalOwned(matches[unitTypeCycleIndex]);
        }

        /// <summary>
        /// Mục tiêu: Chọn mọi unit cùng prefab archetype trên màn hình (Ctrl+Q/W/E/R hoặc double-click).
        /// Cách hoạt động: DeselectAll → quét scene, lọc local owner + viewport + MatchesPrefab → Select.
        /// </summary>
        public void SelectAllUnitsOfKindOnScreen(GameObject referencePrefab)
        {
            if (referencePrefab == null)
            {
                return;
            }

            DeselectAllUnits();

            List<AbstractUnit> matches = CollectUnitsOfKindOnScreen(referencePrefab);
            for (int i = 0; i < matches.Count; i++)
            {
                TrySelectLocalOwned(matches[i]);
            }
        }

        /// <summary>
        /// Mục tiêu: Thu thập unit phe local cùng prefab và đang trong viewport.
        /// Cách hoạt động: Quét scene, lọc owner local + IsSelectableUnitOfKindOnScreen.
        /// </summary>
        List<AbstractUnit> CollectUnitsOfKindOnScreen(GameObject referencePrefab)
        {
            var matches = new List<AbstractUnit>(16);
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                if (IsSelectableUnitOfKindOnScreen(units[i], referencePrefab))
                {
                    matches.Add(units[i]);
                }
            }

            return matches;
        }

        bool IsSelectableUnitOfKindOnScreen(AbstractUnit unit, GameObject referencePrefab) =>
            unit != null
            && unit.gameObject.activeInHierarchy
            && IsOwnedByLocalPlayer(unit)
            && IsCommandableOnScreen(unit)
            && UnitKindMatching.MatchesPrefab(unit, referencePrefab);

        /// <summary>
        /// Mục tiêu: Chọn một nhà cùng loại trên màn hình; bấm lại cùng phím chuyển sang nhà kế tiếp.
        /// Cách hoạt động: Thu thập khớp prefab → DeselectAll → cycle index → Select.
        /// </summary>
        public void SelectOneBuildingOfKindOnScreen(GameObject referencePrefab)
        {
            List<BaseBuilding> matches = CollectBuildingsOfKindOnScreen(referencePrefab);
            if (matches.Count == 0)
            {
                return;
            }

            DeselectAllUnits();

            if (unitTypeCyclePrefab != referencePrefab)
            {
                unitTypeCyclePrefab = referencePrefab;
                unitTypeCycleIndex = -1;
            }

            unitTypeCycleIndex = (unitTypeCycleIndex + 1) % matches.Count;
            TrySelectLocalOwned(matches[unitTypeCycleIndex]);
        }

        /// <summary>
        /// Mục tiêu: Chọn mọi nhà cùng prefab archetype trên màn hình (Ctrl+A/S/D).
        /// Cách hoạt động: DeselectAll → quét scene, lọc local owner + viewport + MatchesPrefab → Select.
        /// </summary>
        public void SelectAllBuildingsOfKindOnScreen(GameObject referencePrefab)
        {
            if (referencePrefab == null)
            {
                return;
            }

            DeselectAllUnits();

            List<BaseBuilding> matches = CollectBuildingsOfKindOnScreen(referencePrefab);
            for (int i = 0; i < matches.Count; i++)
            {
                TrySelectLocalOwned(matches[i]);
            }
        }

        /// <summary>
        /// Mục tiêu: Thu thập nhà phe local cùng prefab và đang trong viewport.
        /// Cách hoạt động: Quét scene, lọc owner local + IsSelectableBuildingOfKindOnScreen.
        /// </summary>
        List<BaseBuilding> CollectBuildingsOfKindOnScreen(GameObject referencePrefab)
        {
            var matches = new List<BaseBuilding>(8);
            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                if (IsSelectableBuildingOfKindOnScreen(buildings[i], referencePrefab))
                {
                    matches.Add(buildings[i]);
                }
            }

            return matches;
        }

        bool IsSelectableBuildingOfKindOnScreen(BaseBuilding building, GameObject referencePrefab) =>
            building != null
            && building.gameObject.activeInHierarchy
            && IsOwnedByLocalPlayer(building)
            && IsCommandableOnScreen(building)
            && BuildingKindMatching.MatchesPrefab(building, referencePrefab);

        /// <summary>
        /// Mục tiêu: Dừng mọi unit đang được chọn (phím H theo 0 A.D.).
        /// Cách hoạt động: Gọi Stop() trên từng AbstractUnit trong selection hiện tại.
        /// </summary>
        public void StopSelectedUnitsFromHotkey()
        {
            EnsureLocalOwnerBeforeHotkey();

            if (selectedUnits.Count == 0)
            {
                return;
            }

            ISelectable[] snapshot = selectedUnits.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is not AbstractUnit unit || !IsOwnedByLocalPlayer(unit))
                {
                    continue;
                }

                if (PlayerInputNetworkBridge.ShouldRelayCommands
                    && PlayerInputNetworkBridge.TryRelayUnitStop != null
                    && PlayerInputNetworkBridge.TryRelayUnitStop(unit))
                {
                    continue;
                }

                unit.Stop();
            }
        }

        private Bounds ResizeSelectionBox()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            float width = mousePosition.x - startingMousePosition.x;
            float height = mousePosition.y - startingMousePosition.y;

            selectionBox.anchoredPosition = startingMousePosition + new Vector2(width / 2, height / 2);
            selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));

            return new Bounds(selectionBox.anchoredPosition, selectionBox.sizeDelta);
        }

        private void HandleRightClick()
        {
            if (selectedUnits.Count == 0) { return; }

            if (!Mouse.current.rightButton.wasReleasedThisFrame)
            {
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!GameplayWorldRaycastUtility.TryGetCommandRaycastHit(
                cameraRay,
                interactableLayers | floorLayers,
                out RaycastHit hit))
            {
                return;
            }

            List<AbstractUnit> abstractUnits = CollectSelectedAbstractUnits();
            TryDispatchCommandsToUnits(abstractUnits, hit, commandBeingActivated: null, MouseButton.Right);
        }

        private List<AbstractUnit> CollectSelectedAbstractUnits()
        {
            List<AbstractUnit> abstractUnits = new(selectedUnits.Count);
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is AbstractUnit unit && IsOwnedByLocalPlayer(unit))
                {
                    abstractUnits.Add(unit);
                }
            }

            return abstractUnits;
        }

        /// <summary>
        /// Mục tiêu: Danh sách commandable phe local đang chọn cho action bar / phím số / lệnh.
        /// Cách hoạt động: Lọc selectedUnits → AbstractCommandable + IsOwnedByLocalPlayer.
        /// </summary>
        private List<AbstractCommandable> CollectSelectedCommandables()
        {
            List<AbstractCommandable> commandables = new(selectedUnits.Count);
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is AbstractCommandable commandable && IsOwnedByLocalPlayer(commandable))
                {
                    commandables.Add(commandable);
                }
            }

            return commandables;
        }

        /// <summary>
        /// Mục tiêu: Right-click / ActivateAction — Move nhóm dùng formation vuông.
        /// Cách hoạt động: Move + &gt;1 unit → <see cref="GroupFormationMoveUtility"/>; còn lại giữ loop lệnh cũ.
        /// </summary>
        private bool TryDispatchCommandsToUnits(
            List<AbstractUnit> abstractUnits,
            RaycastHit hit,
            BaseCommand commandBeingActivated,
            MouseButton mouseButton)
        {
            if (abstractUnits.Count == 0)
            {
                return false;
            }

            if (commandBeingActivated is MoveCommand moveCommand && abstractUnits.Count > 1)
            {
                if (GroupFormationMoveUtility.TryApplyMove(abstractUnits, hit, moveCommand))
                {
                    return true;
                }
            }

            for (int i = 0; i < abstractUnits.Count; i++)
            {
                if (!IsOwnedByLocalPlayer(abstractUnits[i]))
                {
                    continue;
                }

                BaseCommand command = commandBeingActivated;
                if (command == null)
                {
                    if (!AvailableCommandsResolver.TryPickPrimaryRightClickCommand(
                            abstractUnits[i],
                            hit,
                            i,
                            mouseButton,
                            out command))
                    {
                        continue;
                    }

                    if (command is MoveCommand move && abstractUnits.Count > 1)
                    {
                        if (GroupFormationMoveUtility.TryApplyMove(abstractUnits, hit, move))
                        {
                            return true;
                        }
                    }
                }

                CommandContext context = new(abstractUnits[i], hit, i, mouseButton);
                if (!command.CanHandle(context))
                {
                    continue;
                }

                if (PlayerInputNetworkBridge.ShouldRelayCommands
                    && PlayerInputNetworkBridge.TryRelayUnitCommand != null
                    && PlayerInputNetworkBridge.TryRelayUnitCommand(
                        abstractUnits[i],
                        hit,
                        command,
                        mouseButton,
                        i))
                {
                    if (command.IsSingleUnitCommand)
                    {
                        return true;
                    }

                    continue;
                }

                if (command is MoveCommand moveForGroup && abstractUnits.Count > 1)
                {
                    if (GroupFormationMoveUtility.TryApplyMove(abstractUnits, hit, moveForGroup))
                    {
                        return true;
                    }
                }

                command.Handle(context);
                if (command.IsSingleUnitCommand)
                {
                    return true;
                }
            }

            return false;
        }

        private List<BaseCommand> GetAvailableCommands(AbstractUnit unit) => AvailableCommandsResolver.GetFlattened(unit);

        /// <summary>
        /// True khi người chơi đang chọn lệnh cần click thế giới (ghost đặt nhà / v.v.) — không nên đổi hardware cursor.
        /// </summary>
        public bool IsAwaitingWorldCommandClick =>
            (activeCommand != null && activeCommand.RequiresClickToActivate) || placementGhostPinnedToWorld;

        /// <summary>
        /// Mục tiêu: Click chọn đúng unit khi ray chạm cả unit lẫn building (ưu tiên tuyệt đối unit).
        /// Cách hoạt động: Quét mọi hit, chọn AbstractUnit có distance nhỏ nhất; chỉ khi không có unit mới chọn ISelectable khác.
        /// </summary>
        private bool TrySelectClosestUnitFromHits(RaycastHit[] hits)
        {
            AbstractUnit closestUnit = null;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                AbstractUnit unit = hit.collider.GetComponentInParent<AbstractUnit>();
                if (unit != null && IsOwnedByLocalPlayer(unit) && hit.distance < closestDistance)
                {
                    closestUnit = unit;
                    closestDistance = hit.distance;
                }
            }

            if (closestUnit == null)
            {
                return false;
            }

            SelectUnitFromLeftClick(closestUnit);
            return true;
        }

        /// <summary>
        /// Mục tiêu: Click trái chọn một unit hoặc double-click chọn mọi unit cùng loại trên màn hình (0 A.D.).
        /// Cách hoạt động: Hai click liên tiếp trong cửa sổ thời gian lên cùng unit → quét aliveUnits, lọc cùng loại + trong viewport.
        /// </summary>
        private void SelectUnitFromLeftClick(AbstractUnit clickedUnit)
        {
            if (TrySelectAllSameKindOnScreenFromDoubleClick(clickedUnit))
            {
                return;
            }

            TrySelectLocalOwned(clickedUnit);
        }

        /// <summary>
        /// Mục tiêu: Nhận diện double-click trái trên unit để chọn hàng loạt cùng loại.
        /// Cách hoạt động: Hai click trong cửa sổ thời gian, cùng loại (có thể hai instance khác nhau) → thay thế selection.
        /// </summary>
        private bool TrySelectAllSameKindOnScreenFromDoubleClick(AbstractUnit clickedUnit)
        {
            float now = Time.unscaledTime;
            bool isDoubleClick = lastUnitLeftClickTarget != null
                && UnitKindMatching.IsSameKind(lastUnitLeftClickTarget, clickedUnit)
                && lastUnitLeftClickTime >= 0f
                && now - lastUnitLeftClickTime <= UnitDoubleClickIntervalSeconds;

            lastUnitLeftClickTime = now;
            lastUnitLeftClickTarget = clickedUnit;

            if (!isDoubleClick)
            {
                return false;
            }

            SelectAllSameKindOnScreen(clickedUnit);
            return true;
        }

        /// <summary>
        /// Mục tiêu: Chọn mọi unit cùng loại trên màn hình — thay thế toàn bộ selection (kể cả khi giữ Shift).
        /// Cách hoạt động: DeselectAll → quét aliveUnits, lọc owner/viewport/UnitKindMatching → Select từng unit khớp.
        /// </summary>
        private void SelectAllSameKindOnScreen(AbstractUnit referenceUnit)
        {
            if (referenceUnit?.UnitSO?.Prefab == null)
            {
                return;
            }

            SelectAllUnitsOfKindOnScreen(referenceUnit.UnitSO.Prefab);
        }

        /// <summary>
        /// Mục tiêu: Kiểm tra unit có projection nằm trong viewport camera hay không.
        /// Cách hoạt động: Ủy quyền <see cref="IsCommandableOnScreen"/>.
        /// </summary>
        private bool IsUnitOnScreen(AbstractUnit unit) => IsCommandableOnScreen(unit);

        /// <summary>
        /// Mục tiêu: Kiểm tra commandable có projection nằm trong viewport camera hay không.
        /// Cách hoạt động: WorldToViewportPoint; z &gt; 0 và tọa độ x/y trong [0, 1].
        /// </summary>
        private bool IsCommandableOnScreen(AbstractCommandable commandable)
        {
            if (camera == null || commandable == null)
            {
                return false;
            }

            Vector3 viewport = camera.WorldToViewportPoint(commandable.transform.position);
            return viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }

        private void ResetUnitDoubleClickTracking()
        {
            lastUnitLeftClickTime = -1f;
            lastUnitLeftClickTarget = null;
        }

        private void HandleLeftClick()
        {
            if (camera == null) { return ; }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (activeCommand == null)
            {
                RaycastHit[] hits = Physics.RaycastAll(
                    cameraRay,
                    float.MaxValue,
                    selectableUnitsLayers,
                    QueryTriggerInteraction.Collide);
                if (hits.Length == 0)
                {
                    return;
                }

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                if (TrySelectClosestUnitFromHits(hits))
                {
                    return;
                }

                foreach (RaycastHit h in hits)
                {
                    if (h.collider.GetComponentInParent<AbstractUnit>() != null)
                    {
                        continue;
                    }

                    AbstractCommandable commandable = h.collider.GetComponentInParent<AbstractCommandable>();
                    if (commandable is ISelectable && IsOwnedByLocalPlayer(commandable))
                    {
                        TrySelectLocalOwned(commandable);
                        return;
                    }
                }
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject()
                && GameplayWorldRaycastUtility.TryGetCommandRaycastHit(
                    cameraRay,
                    interactableLayers | floorLayers,
                    out RaycastHit hit))
            {
                ActivateAction(hit);
            }
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ owner local và loại selection địch trước mọi hotkey gameplay.
        /// Cách hoạt động: ResolveLocalOwner → PurgeNonLocalFromSelection.
        /// </summary>
        void EnsureLocalOwnerBeforeHotkey()
        {
            ResolveLocalOwner();
            PurgeNonLocalFromSelection();
        }

        /// <summary>
        /// Mục tiêu: Loại unit/nhà địch khỏi selection nội bộ PlayerInput.
        /// Cách hoạt động: Deselect và remove mọi entry không pass IsLocalOwnedSelectable.
        /// </summary>
        void PurgeNonLocalFromSelection()
        {
            for (int i = selectedUnits.Count - 1; i >= 0; i--)
            {
                ISelectable selectable = selectedUnits[i];
                if (IsLocalOwnedSelectable(selectable))
                {
                    continue;
                }

                selectable?.Deselect();
                selectedUnits.RemoveAt(i);
            }
        }

        /// <summary>
        /// Mục tiêu: Kiểm tra selectable có phải commandable thuộc phe local.
        /// Cách hoạt động: AbstractCommandable + IsOwnedByLocalPlayer.
        /// </summary>
        bool IsLocalOwnedSelectable(ISelectable selectable) =>
            selectable is AbstractCommandable commandable && IsOwnedByLocalPlayer(commandable);

        /// <summary>
        /// Mục tiêu: Luôn lấy owner human trên máy này — tránh cache <see cref="localOwner"/> lệch (MP / debug F1-F2).
        /// Cách hoạt động: Đọc <see cref="LocalHumanOwnerAccess"/> và cập nhật field localOwner.
        /// </summary>
        Owner ResolveLocalOwner()
        {
            localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            return localOwner;
        }

        /// <summary>
        /// Mục tiêu: Index unit/nhà phe local đã spawn trước khi PlayerInput subscribe Bus.
        /// Cách hoạt động: FindObjectsByType, chỉ thêm entity có Owner == ResolveLocalOwner().
        /// </summary>
        void RegisterExistingLocalCommandables()
        {
            Owner local = ResolveLocalOwner();

            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit != null && unit.Owner == local)
                {
                    aliveUnits.Add(unit);
                }
            }

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building != null && building.Owner == local)
                {
                    aliveBuildings.Add(building);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Chỉ gọi Select khi commandable thuộc phe local — chặn chọn nhà/unit địch qua hotkey.
        /// Cách hoạt động: Kiểm tra IsOwnedByLocalPlayer trước khi gọi Select().
        /// </summary>
        void TrySelectLocalOwned(AbstractCommandable commandable)
        {
            if (!IsOwnedByLocalPlayer(commandable))
            {
                return;
            }

            commandable.Select();
        }

        /// <summary>
        /// Mục tiêu: Chỉ cho phép chọn unit/nhà thuộc phe người chơi local.
        /// Cách hoạt động: So sánh <see cref="AbstractCommandable.Owner"/> với ResolveLocalOwner().
        /// </summary>
        private bool IsOwnedByLocalPlayer(AbstractCommandable commandable) =>
            commandable != null && commandable.Owner == ResolveLocalOwner();

        private void ActivateAction(RaycastHit hit)
        {
            BaseCommand commandBeingActivated = activeCommand;
            bool deferDestroyGhostForBuild = commandBeingActivated is BuildBuildingCommand && ghostInstance != null;

            if (ghostInstance != null && !deferDestroyGhostForBuild)
            {
                DisposePlacementGhost();
            }

            List<AbstractUnit> abstractUnits = CollectSelectedAbstractUnits();
            bool buildDispatched = false;

            List<AbstractCommandable> abstractCommandables = CollectSelectedCommandables();

            if (commandBeingActivated is MoveCommand moveCommand
                && abstractUnits.Count > 1
                && GroupFormationMoveUtility.TryApplyMove(abstractUnits, hit, moveCommand))
            {
                buildDispatched = true;
            }
            else
            {
                for (int i = 0; i < abstractCommandables.Count; i++)
                {
                    CommandContext context = new(abstractCommandables[i], hit, i);
                    if (commandBeingActivated.CanHandle(context))
                    {
                        commandBeingActivated.Handle(context);
                        buildDispatched = true;
                        if (commandBeingActivated.IsSingleUnitCommand)
                        {
                            break;
                        }
                    }
                }
            }

            SetActiveCommand(null);

            if (deferDestroyGhostForBuild)
            {
                if (buildDispatched && ghostInstance != null)
                {
                    placementGhostPinnedToWorld = true;
                    placementGhostPinnedPosition = hit.point;
                    pinnedPlacementRestrictionsCommand = commandBeingActivated;
                    ghostInstance.transform.position = placementGhostPinnedPosition;
                    UpdateGhostPlacementVisual(EvaluateGhostPlacementValid(placementGhostPinnedPosition, pinnedPlacementRestrictionsCommand));
                }
                else if (commandBeingActivated is BuildBuildingCommand buildCommand
                    && !SupplyAffordability.HasEnough(ResolveLocalOwner(), buildCommand.Building.Cost))
                {
                    SupplyAffordability.WarnPlayerIfInsufficient(
                        ResolveLocalOwner(),
                        buildCommand.Building.Cost,
                        $"xây {buildCommand.Building.Name}");
                    DisposePlacementGhost();
                }
                else if (ghostInstance != null)
                {
                    DisposePlacementGhost();
                }
            }
        }

        private void HandlePanning()
        {
            if (cameraTarget == null)
            {
                return;
            }

            Vector2 moveAmount = GetKeyboardMoveAmount();
            moveAmount += GetMouseMoveAmount();

            if (cameraTarget != null && cameraConfig.EnablePanLimits)
            {
                const float edgeEps = 0.05f;
                Vector3 p = cameraTarget.position;
                if (p.x <= cameraConfig.PanLimitMinXZ.x + edgeEps && moveAmount.x < 0f)
                {
                    moveAmount.x = 0f;
                }

                if (p.x >= cameraConfig.PanLimitMaxXZ.x - edgeEps && moveAmount.x > 0f)
                {
                    moveAmount.x = 0f;
                }

                if (p.z <= cameraConfig.PanLimitMinXZ.y + edgeEps && moveAmount.y < 0f)
                {
                    moveAmount.y = 0f;
                }

                if (p.z >= cameraConfig.PanLimitMaxXZ.y - edgeEps && moveAmount.y > 0f)
                {
                    moveAmount.y = 0f;
                }
            }

            cameraTarget.linearVelocity = new Vector3(moveAmount.x, 0, moveAmount.y);
        }

        private Vector2 GetMouseMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (!cameraConfig.EnableEdgePan) { return moveAmount; }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            float edgeBorder = cameraConfig.GetEdgePanBorderPixels();

            if (mousePosition.x <= edgeBorder)
            {
                moveAmount.x -= cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.x >= screenWidth - edgeBorder)
            {
                moveAmount.x += cameraConfig.MousePanSpeed;
            }

            if (mousePosition.y >= screenHeight - edgeBorder)
            {
                moveAmount.y += cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.y <= edgeBorder)
            {
                moveAmount.y -= cameraConfig.MousePanSpeed;
            }

            return moveAmount;
        }

        private Vector2 GetKeyboardMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (Keyboard.current.upArrowKey.isPressed)
            {
                moveAmount.y += cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                moveAmount.x -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.downArrowKey.isPressed)
            {
                moveAmount.y -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.rightArrowKey.isPressed)
            {
                moveAmount.x += cameraConfig.KeyboardPanSpeed;
            }

            return moveAmount;
        }

        public Transform CameraTargetTransform => cameraTarget != null ? cameraTarget.transform : null;

        public Camera GameplayCamera => camera;

        /// <summary>
        /// Mục tiêu: Di chuyển camera tới điểm world khi click minimap.
        /// Cách hoạt động: Cập nhật XZ của Rigidbody cameraTarget và clamp theo PanLimit.
        /// </summary>
        public void PanCameraToWorldPosition(Vector3 worldPosition)
        {
            if (cameraTarget == null)
            {
                return;
            }

            Vector3 position = cameraTarget.position;
            position.x = worldPosition.x;
            position.z = worldPosition.z;

            if (cameraConfig.EnablePanLimits)
            {
                position.x = Mathf.Clamp(position.x, cameraConfig.PanLimitMinXZ.x, cameraConfig.PanLimitMaxXZ.x);
                position.z = Mathf.Clamp(position.z, cameraConfig.PanLimitMinXZ.y, cameraConfig.PanLimitMaxXZ.y);
            }

            cameraTarget.MovePosition(position);
        }
    }
}
