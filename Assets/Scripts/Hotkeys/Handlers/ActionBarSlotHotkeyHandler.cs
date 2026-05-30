using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Phím 1–9 → kích hoạt lệnh slot tương ứng trên unit đang chọn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionBarSlotHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour actionBarTargetBehaviour;

        IHotkeyActionBarTarget actionBarTarget;

        public override HotkeyId Id => HotkeyId.ActionBarSlot;

        void Awake()
        {
            actionBarTarget = actionBarTargetBehaviour as IHotkeyActionBarTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyActionBarTarget>(gameObject);
        }

        /// <summary>
        /// Mục tiêu: Map phím số hàng trên → slot action bar.
        /// Cách hoạt động: Digit1–9 → slot 0–8; gọi target nếu hợp lệ.
        /// </summary>
        public override void Execute(in HotkeyContext context)
        {
            if (actionBarTarget == null)
            {
                return;
            }

            if (!TryGetSlotIndex(context.MatchedChord.key, out int slotIndex))
            {
                return;
            }

            actionBarTarget.OnHotkeyActionBarSlot(slotIndex);
        }

        static bool TryGetSlotIndex(Key key, out int slotIndex)
        {
            if (key >= Key.Digit1 && key <= Key.Digit9)
            {
                slotIndex = key - Key.Digit1;
                return true;
            }

            slotIndex = -1;
            return false;
        }
    }
}
