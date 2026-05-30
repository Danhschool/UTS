using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Esc → gọi IHotkeyCancelTarget. Mẫu handler mở rộng theo từng HotkeyId.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CancelSelectionHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour cancelTargetBehaviour;

        IHotkeyCancelTarget cancelTarget;

        public override HotkeyId Id => HotkeyId.CancelSelection;

        void Awake()
        {
            cancelTarget = cancelTargetBehaviour as IHotkeyCancelTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyCancelTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            cancelTarget?.OnHotkeyCancel(in context);
        }
    }
}
