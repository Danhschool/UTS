using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Shift+Delete → xóa ngay selection phe local.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeleteSelectionImmediateHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour deleteTargetBehaviour;

        IHotkeyDeleteSelectionTarget deleteTarget;

        public override HotkeyId Id => HotkeyId.DeleteSelectionImmediate;

        void Awake()
        {
            deleteTarget = deleteTargetBehaviour as IHotkeyDeleteSelectionTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyDeleteSelectionTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            deleteTarget?.OnHotkeyDeleteSelection(in context, immediate: true);
        }
    }
}
