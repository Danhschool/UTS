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

        private Owner localOwner = Owner.Player1;

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

            localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            SubscribeBus(localOwner);
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
        }

        private void OnDestroy()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            UnsubscribeBus(localOwner);
            ForceSystemDefaultCursor();
            DestroyRuntimeCursorTexture(gatherCursorBaked);
            DestroyRuntimeCursorTexture(attackCursorBaked);
            gatherCursorBaked = null;
            attackCursorBaked = null;
        }

        void OnLocalOwnerChanged(Owner owner)
        {
            UnsubscribeBus(localOwner);
            localOwner = owner;
            selectionMirror.Clear();
            selectedUnitsBuffer.Clear();
            SubscribeBus(localOwner);
        }

        void SubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] += OnUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += OnUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] += OnUnitDeath;
        }

        void UnsubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] -= OnUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= OnUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] -= OnUnitDeath;
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
        /// Mục tiêu: chọn cursor gather/attack theo lệnh thắng khi chuột phải, bỏ qua Move/Stop (luôn CanHandle).
        /// Cách hoạt động: quét toàn bộ AvailableCommands; chỉ Gather/Attack quyết định cursor, ưu tiên Gather trước Attack.
        /// </summary>
        private HoverCursorEvaluation EvaluateHoverCursorForUnit(AbstractUnit unit, RaycastHit hit)
        {
            if (!AvailableCommandsResolver.TryPickPrimaryRightClickCommand(
                    unit,
                    hit,
                    0,
                    MouseButton.Right,
                    out BaseCommand picked))
            {
                return HoverCursorEvaluation.SystemDefault;
            }

            if (picked is GatherCommand)
            {
                return PassesOptionalLayerMask(gatherCursorLayerFilter, hit.collider.gameObject.layer)
                    ? HoverCursorEvaluation.Gather
                    : HoverCursorEvaluation.SystemDefault;
            }

            if (picked is AttackCommand)
            {
                return HoverCursorEvaluation.Attack;
            }

            return HoverCursorEvaluation.SystemDefault;
        }

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

        private void TransitionToSystemDefault()
        {
            if (lastApplied == AppliedCursorKind.SystemDefault || lastApplied == AppliedCursorKind.None)
            {
                return;
            }

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            lastApplied = AppliedCursorKind.SystemDefault;
        }

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

        private static void DestroyRuntimeCursorTexture(Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

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
