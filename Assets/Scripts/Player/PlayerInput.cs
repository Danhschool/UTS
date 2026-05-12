using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Commands;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Rigidbody cameraTarget;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private new Camera camera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private LayerMask selectableUnitsLayers;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private LayerMask floorLayers;
        [SerializeField] private RectTransform selectionBox;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorTintColor = Color.red;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color errorFresnelColor = new (4, 1.7f, 0, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceTintColor = new (0.2f, 0.65f, 1, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)]
        private Color availableToPlaceFresnelColor = new(4, 1.7f, 0, 2);

        private Vector2 startingMousePosition;

        private BaseCommand activeCommand;
        private GameObject ghostInstance;
        private MeshRenderer ghostRenderer;
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
        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL = Shader.PropertyToID("_FresnelColor");

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

            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] += HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
        }

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
        }

        private void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }
        private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);
        private void HandleUnitSpawn(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);
        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            activeCommand = evt.Command;
            if (!activeCommand.RequiresClickToActivate)
            {
                ActivateAction(new RaycastHit());
            }
            else if (activeCommand.GhostPrefab != null)
            {
                ghostInstance = Instantiate(activeCommand.GhostPrefab);
                ghostRenderer = ghostInstance.GetComponentInChildren<MeshRenderer>();
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
                Destroy(ghostInstance);
                ghostInstance = null;
                activeCommand = null;
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                ghostInstance.transform.position = hit.point;

                bool allRestrictionsPass = activeCommand.AllRestrictionsPass(hit.point);

                ghostRenderer.material.SetColor(TINT, allRestrictionsPass ? availableToPlaceTintColor : errorTintColor);
                ghostRenderer.material.SetColor(FRESNEL,
                    allRestrictionsPass ? availableToPlaceFresnelColor : errorFresnelColor
                );
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

            HandleLeftClick();
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
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, interactableLayers | floorLayers))
            {
                List<AbstractUnit> abstractUnits = new (selectedUnits.Count);
                foreach(ISelectable selectable in selectedUnits)
                {
                    if (selectable is AbstractUnit unit)
                    {
                        abstractUnits.Add(unit);
                    }
                }

                for(int i = 0; i < abstractUnits.Count; i++)
                {
                    CommandContext context = new(abstractUnits[i], hit, i, MouseButton.Right);
                    List<BaseCommand> availableCommands = GetAvailableCommands(abstractUnits[i]);

                    foreach(ICommand command in availableCommands)
                    {
                        if (command.CanHandle(context))
                        {
                            command.Handle(context);
                            if (command.IsSingleUnitCommand)
                            {
                                return;
                            }
                            break;
                        }
                    }
                }
            }
        }

        private List<BaseCommand> GetAvailableCommands(AbstractUnit unit)
        {
            OverrideCommandsCommand[] overrideCommandsCommands = unit.AvailableCommands
                .Where(command => command is OverrideCommandsCommand)
                .Cast<OverrideCommandsCommand>()
                .ToArray();

            List<BaseCommand> allAvailableCommands = new();
            foreach(OverrideCommandsCommand overrideCommand in overrideCommandsCommands)
            {
                allAvailableCommands.AddRange(overrideCommand.Commands
                    .Where(command => command is not OverrideCommandsCommand)
                );
            }

            allAvailableCommands.AddRange(unit.AvailableCommands
                .Where(command => command is not OverrideCommandsCommand)
            );

            return allAvailableCommands;
        }

        private void HandleLeftClick()
        {
            if (camera == null) { return ; }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (activeCommand == null
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayers)
                && hit.collider.TryGetComponent(out ISelectable selectable))
            {
                selectable.Select();
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject()
                && Physics.Raycast(cameraRay, out hit, float.MaxValue, interactableLayers | floorLayers))
            {
                ActivateAction(hit);
            }
        }

        private void ActivateAction(RaycastHit hit)
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }

            List<AbstractCommandable> abstractCommandables = selectedUnits
                                .Where((unit) => unit is AbstractCommandable)
                                .Cast<AbstractCommandable>()
                                .ToList();

            for (int i = 0; i < abstractCommandables.Count; i++)
            {
                CommandContext context = new(abstractCommandables[i], hit, i);
                if (activeCommand.CanHandle(context))
                {
                    activeCommand.Handle(context);
                    if (activeCommand.IsSingleUnitCommand)
                    {
                        break;
                    }
                }
            }

            activeCommand = null;
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

            if (mousePosition.x <= cameraConfig.EdgePanSize)
            {
                moveAmount.x -= cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.x >= screenWidth - cameraConfig.EdgePanSize)
            {
                moveAmount.x += cameraConfig.MousePanSpeed;
            }

            if (mousePosition.y >= screenHeight - cameraConfig.EdgePanSize)
            {
                moveAmount.y += cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.y <= cameraConfig.EdgePanSize)
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
    }
}
