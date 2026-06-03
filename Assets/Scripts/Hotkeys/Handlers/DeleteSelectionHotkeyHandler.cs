using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Delete → xóa selection phe local (chậm / có hiệu ứng Die).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeleteSelectionHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour deleteTargetBehaviour;

        IHotkeyDeleteSelectionTarget deleteTarget;

        public override HotkeyId Id => HotkeyId.DeleteSelection;

        void Awake()
        {
            deleteTarget = deleteTargetBehaviour as IHotkeyDeleteSelectionTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyDeleteSelectionTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            deleteTarget?.OnHotkeyDeleteSelection(in context, immediate: false);
        }
    }
}
