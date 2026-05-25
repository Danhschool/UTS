using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Utilities;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    public class PlayerInput : MonoBehaviour, IMinimapCameraNavigator
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
        private float orthoSizeVelocity;
        private float smoothedFollowZ;
        private float followZVelocity;
        private Owner localOwner = Owner.Player1;

        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

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
            scrollZoomScale = smoothedZoomScale = zoomTargetScale = ComputeInitialZoomScaleFromFollowOffset();

            localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            SubscribeBus(localOwner);
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
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
            selectedUnits.Clear();
            SubscribeBus(localOwner);
        }

        void SubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[owner] += HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[owner] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleUnitDeath;
            Bus<BuildingConstructStartedEvent>.OnEvent[owner] += OnBuildingConstructStarted;
        }

        void UnsubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[owner] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[owner] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleUnitDeath;
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
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);
        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit.Owner == localOwner)
            {
                aliveUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            DisposePlacementGhost();
            activeCommand = evt.Command;
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

        private void Update()
        {
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

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                DisposePlacementGhost();
                activeCommand = null;
                return;
            }

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
                unit.Select();
            }
            selectionBox.gameObject.SetActive(false);
        }

        private void HandleMouseDrag()
        {
            if (activeCommand != null || wasMouseDownOnUI) return;

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

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Mouse.current.rightButton.wasReleasedThisFrame
                && Physics.Raycast(
                    cameraRay,
                    out RaycastHit hit,
                    float.MaxValue,
                    interactableLayers | floorLayers,
                    QueryTriggerInteraction.Collide))
            {
                List<AbstractUnit> abstractUnits = CollectSelectedAbstractUnits();
                TryDispatchCommandsToUnits(abstractUnits, hit, commandBeingActivated: null, MouseButton.Right);
            }
        }

        private List<AbstractUnit> CollectSelectedAbstractUnits()
        {
            List<AbstractUnit> abstractUnits = new(selectedUnits.Count);
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is AbstractUnit unit)
                {
                    abstractUnits.Add(unit);
                }
            }

            return abstractUnits;
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

            if (commandBeingActivated is RallyAreaCommand rallyCommand
                && RallyAreaCommand.TryApplyRally(abstractUnits[0], hit, rallyCommand))
            {
                return true;
            }

            for (int i = 0; i < abstractUnits.Count; i++)
            {
                BaseCommand command = commandBeingActivated;
                if (command == null)
                {
                    List<BaseCommand> availableCommands = GetAvailableCommands(abstractUnits[i]);
                    bool dispatched = false;
                    foreach (ICommand candidate in availableCommands)
                    {
                        CommandContext probe = new(abstractUnits[i], hit, i, mouseButton);
                        if (candidate is not BaseCommand baseCommand || !baseCommand.CanHandle(probe))
                        {
                            continue;
                        }

                        if (baseCommand is MoveCommand move && abstractUnits.Count > 1)
                        {
                            if (GroupFormationMoveUtility.TryApplyMove(abstractUnits, hit, move))
                            {
                                return true;
                            }
                        }

                        if (baseCommand is RallyAreaCommand rally
                            && RallyAreaCommand.TryApplyRally(abstractUnits[0], hit, rally))
                        {
                            return true;
                        }

                        command = baseCommand;
                        dispatched = true;
                        break;
                    }

                    if (!dispatched)
                    {
                        continue;
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

                if (command is RallyAreaCommand rallyForGroup
                    && RallyAreaCommand.TryApplyRally(abstractUnits[0], hit, rallyForGroup))
                {
                    return true;
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

            closestUnit.Select();
            return true;
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
                    if (commandable is ISelectable selectable && IsOwnedByLocalPlayer(commandable))
                    {
                        selectable.Select();
                        return;
                    }
                }
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject()
                && Physics.Raycast(
                    cameraRay,
                    out RaycastHit hit,
                    float.MaxValue,
                    interactableLayers | floorLayers,
                    QueryTriggerInteraction.Collide))
            {
                ActivateAction(hit);
            }
        }

        /// <summary>
        /// Mục tiêu: Chỉ cho phép chọn unit/nhà thuộc phe người chơi local.
        /// Cách hoạt động: So sánh <see cref="AbstractCommandable.Owner"/> với <see cref="localOwner"/>.
        /// </summary>
        private bool IsOwnedByLocalPlayer(AbstractCommandable commandable) =>
            commandable != null && commandable.Owner == localOwner;

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

            List<AbstractCommandable> abstractCommandables = selectedUnits
                .Where(unit => unit is AbstractCommandable)
                .Cast<AbstractCommandable>()
                .ToList();

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

            activeCommand = null;

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
                    && !SupplyAffordability.HasEnough(localOwner, buildCommand.Building.Cost))
                {
                    SupplyAffordability.WarnPlayerIfInsufficient(
                        localOwner,
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
