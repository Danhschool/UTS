using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Khi có ít nhất một <see cref="AbstractUnit"/> được chọn: raycast theo chuột trên world;
    /// nếu với <b>mọi</b> unit đã chọn, lệnh thắng khi chuột phải cùng là <see cref="GatherCommand"/> hoặc cùng là <see cref="AttackCommand"/>
    /// (cùng quy tắc thứ tự <c>CanHandle</c> như click phải) thì đổi cursor tương ứng — nhiều worker cùng khai thác vẫn thấy gather cursor.
    /// Vật có <see cref="IHideable"/> và <c>IsVisible == false</c> thì bỏ qua hit đó.
    /// </summary>
    public sealed class UnitSelectionHoverCursor : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Camera gameCamera;
        [Tooltip("Nên trùng với interactableLayers | floorLayers trên PlayerInput (raycast chuột phải).")]
        [SerializeField] private LayerMask worldHoverLayers;
        [Tooltip("Để trống = không lọc. Gán layer: chỉ đổi cursor gather khi collider trúng ray nằm trên một trong các layer này.")]
        [SerializeField] private LayerMask gatherCursorLayerFilter;
        [Tooltip("Để trống = không lọc. Gán layer: chỉ đổi cursor attack khi collider trúng ray nằm trên một trong các layer này.")]
        [SerializeField] private LayerMask attackCursorLayerFilter;
        [SerializeField] private Texture2D gatherCursorTexture;
        [SerializeField] private Vector2 gatherHotspot;
        [SerializeField] private Texture2D attackCursorTexture;
        [SerializeField] private Vector2 attackHotspot;

        private Texture2D gatherCursorBaked;
        private Texture2D attackCursorBaked;

        private readonly List<ISelectable> selectionMirror = new(16);
        private readonly List<AbstractUnit> selectedUnitsBuffer = new(16);

        private enum HoverCursorEvaluation
        {
            SystemDefault,
            Gather,
            Attack
        }

        private enum AppliedCursorKind
        {
            None,
            SystemDefault,
            Gather,
            Attack
        }

        private AppliedCursorKind lastApplied = AppliedCursorKind.None;

        private void Awake()
        {
            gatherCursorBaked = SystemCursorTextureBaker.BakeForSystemCursor(gatherCursorTexture);
            attackCursorBaked = SystemCursorTextureBaker.BakeForSystemCursor(attackCursorTexture);

            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += OnUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += OnUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += OnUnitDeath;
        }

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= OnUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= OnUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= OnUnitDeath;
            ForceSystemDefaultCursor();
            DestroyRuntimeCursorTexture(gatherCursorBaked);
            DestroyRuntimeCursorTexture(attackCursorBaked);
            gatherCursorBaked = null;
            attackCursorBaked = null;
        }

        private void OnDisable()
        {
            ForceSystemDefaultCursor();
        }

        private void LateUpdate()
        {
            if (gameCamera == null || Mouse.current == null)
            {
                return;
            }

            if (playerInput != null && playerInput.IsAwaitingWorldCommandClick)
            {
                TransitionToSystemDefault();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                TransitionToSystemDefault();
                return;
            }

            CollectSelectedAbstractUnits();
            if (selectedUnitsBuffer.Count == 0)
            {
                TransitionToSystemDefault();
                return;
            }

            Ray ray = gameCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                float.MaxValue,
                worldHoverLayers,
                QueryTriggerInteraction.Collide);
            if (hits.Length == 0)
            {
                TransitionToSystemDefault();
                return;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            if (!TryGetFirstVisibilityEligibleHit(hits, out RaycastHit hit))
            {
                TransitionToSystemDefault();
                return;
            }

            HoverCursorEvaluation first = EvaluateHoverCursorForUnit(selectedUnitsBuffer[0], hit);
            if (first == HoverCursorEvaluation.SystemDefault)
            {
                TransitionToSystemDefault();
                return;
            }

            for (int i = 1; i < selectedUnitsBuffer.Count; i++)
            {
                if (EvaluateHoverCursorForUnit(selectedUnitsBuffer[i], hit) != first)
                {
                    TransitionToSystemDefault();
                    return;
                }
            }

            if (first == HoverCursorEvaluation.Gather)
            {
                ApplyGatherCursor();
                return;
            }

            ApplyAttackCursor();
        }

        /// <summary>
        /// Mục tiêu: Đổ đệm các <see cref="AbstractUnit"/> đang được chọn để xử lý hover mà không cấp phát mỗi frame.
        /// Cách hoạt động: Xóa buffer rồi thêm mọi phần tử trong <see cref="selectionMirror"/> kiểu <see cref="AbstractUnit"/>.
        /// </summary>
        private void CollectSelectedAbstractUnits()
        {
            selectedUnitsBuffer.Clear();
            for (int i = 0; i < selectionMirror.Count; i++)
            {
                if (selectionMirror[i] is AbstractUnit unit)
                {
                    selectedUnitsBuffer.Add(unit);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Với một unit và một hit raycast, xác định cursor gather/attack có áp được không (giống vòng lệnh trong <c>LateUpdate</c> trước đây).
        /// Cách hoạt động: Dựng <see cref="CommandContext"/> chuột phải, duyệt <see cref="AvailableCommandsResolver.GetFlattened"/>; lệnh đầu tiên <c>CanHandle</c> quyết định Gather, Attack hoặc mặc định (kể cả khi layer filter từ chối gather/attack thì thử lệnh tiếp theo).
        /// </summary>
        private HoverCursorEvaluation EvaluateHoverCursorForUnit(AbstractUnit unit, RaycastHit hit)
        {
            CommandContext context = new(unit.Owner, unit, hit, 0, MouseButton.Right);
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            foreach (BaseCommand command in commands)
            {
                if (!command.CanHandle(context))
                {
                    continue;
                }

                if (command is GatherCommand)
                {
                    if (PassesOptionalLayerMask(gatherCursorLayerFilter, hit.collider.gameObject.layer))
                    {
                        return HoverCursorEvaluation.Gather;
                    }

                    continue;
                }

                if (command is AttackCommand)
                {
                    if (PassesOptionalLayerMask(attackCursorLayerFilter, hit.collider.gameObject.layer))
                    {
                        return HoverCursorEvaluation.Attack;
                    }

                    continue;
                }

                return HoverCursorEvaluation.SystemDefault;
            }

            return HoverCursorEvaluation.SystemDefault;
        }

        /// <summary>
        /// Mục tiêu: Áp texture gather khi lệnh gather sẽ thắng chuột phải.
        /// Cách hoạt động: Nếu có texture thì <see cref="Cursor.SetCursor"/> một lần khi đổi trạng thái; không thì về cursor hệ thống.
        /// </summary>
        private void ApplyGatherCursor()
        {
            if (gatherCursorBaked == null)
            {
                TransitionToSystemDefault();
                return;
            }

            if (lastApplied == AppliedCursorKind.Gather)
            {
                return;
            }

            Cursor.SetCursor(gatherCursorBaked, gatherHotspot, CursorMode.Auto);
            lastApplied = AppliedCursorKind.Gather;
        }

        /// <summary>
        /// Mục tiêu: Áp texture attack khi lệnh attack sẽ thắng chuột phải.
        /// Cách hoạt động: Giống gather — set cursor khi đổi trạng thái, bỏ qua nếu chưa gán texture.
        /// </summary>
        private void ApplyAttackCursor()
        {
            if (attackCursorBaked == null)
            {
                TransitionToSystemDefault();
                return;
            }

            if (lastApplied == AppliedCursorKind.Attack)
            {
                return;
            }

            Cursor.SetCursor(attackCursorBaked, attackHotspot, CursorMode.Auto);
            lastApplied = AppliedCursorKind.Attack;
        }

        /// <summary>
        /// Mục tiêu: Trả cursor về mặc định OS khi rời mục tiêu gather/attack hoặc không còn đủ điều kiện hover.
        /// Cách hoạt động: Gọi <see cref="Cursor.SetCursor"/> với null chỉ khi trước đó đang dùng texture tùy chỉnh.
        /// </summary>
        private void TransitionToSystemDefault()
        {
            if (lastApplied == AppliedCursorKind.SystemDefault || lastApplied == AppliedCursorKind.None)
            {
                return;
            }

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            lastApplied = AppliedCursorKind.SystemDefault;
        }

        /// <summary>
        /// Mục tiêu: Luôn xóa cursor tùy chỉnh khi hủy component (tránh để cursor lạ sau khi thoát Play).
        /// Cách hoạt động: Gọi SetCursor(null) không điều kiện và reset cờ nội bộ.
        /// </summary>
        private void ForceSystemDefaultCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            lastApplied = AppliedCursorKind.None;
        }

        private void OnUnitSelected(UnitSelectedEvent evt)
        {
            if (!selectionMirror.Contains(evt.Unit))
            {
                selectionMirror.Add(evt.Unit);
            }
        }

        private void OnUnitDeselected(UnitDeselectedEvent evt) => selectionMirror.Remove(evt.Unit);

        private void OnUnitDeath(UnitDeathEvent evt) => selectionMirror.Remove(evt.Unit);

        /// <summary>
        /// Mục tiêu: Giải phóng texture runtime do baker tạo, tránh rò bộ nhớ.
        /// Cách hoạt động: Chỉ <see cref="Object.Destroy"/> khi tham chiếu khác null.
        /// </summary>
        private static void DestroyRuntimeCursorTexture(Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        /// <summary>
        /// Mục tiêu: Chọn hit đầu tiên không thuộc vật đang ẩn (fog) theo <see cref="IHideable.IsVisible"/>.
        /// Cách hoạt động: Duyệt theo khoảng cách; bỏ qua collider có <see cref="IHideable"/> trên self/parent với <c>IsVisible == false</c>; không có interface thì coi là được dùng.
        /// </summary>
        private static bool TryGetFirstVisibilityEligibleHit(RaycastHit[] sortedHits, out RaycastHit hit)
        {
            for (int i = 0; i < sortedHits.Length; i++)
            {
                if (IsRayHitEligibleForCommandCursor(sortedHits[i]))
                {
                    hit = sortedHits[i];
                    return true;
                }
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Mục tiêu: Không dùng hit lên vật bị ẩn để quyết định cursor gather/attack.
        /// Cách hoạt động: <see cref="Component.GetComponentInParent{T}"/> tìm <see cref="IHideable"/>; nếu có và <see cref="IHideable.IsVisible"/> false thì loại hit này.
        /// </summary>
        private static bool IsRayHitEligibleForCommandCursor(RaycastHit h)
        {
            if (h.collider == null)
            {
                return false;
            }

            IHideable hideable = h.collider.GetComponentInParent<IHideable>();
            if (hideable == null)
            {
                return true;
            }

            return hideable.IsVisible;
        }

        /// <summary>
        /// Mục tiêu: Lọc layer tùy chọn cho icon cursor; mask = 0 nghĩa là không giới hạn layer (giữ hành vi cũ).
        /// Cách hoạt động: Nếu <paramref name="mask"/> có bit thì collider phải thuộc một layer trong mask.
        /// </summary>
        private static bool PassesOptionalLayerMask(LayerMask mask, int gameObjectLayer)
        {
            if (mask.value == 0)
            {
                return true;
            }

            return (mask.value & (1 << gameObjectLayer)) != 0;
        }
    }
}
