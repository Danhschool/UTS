using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// F → pan camera tới unit/nhà phe local đang chọn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollowUnitHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour cameraTargetBehaviour;

        IHotkeyCameraTarget cameraTarget;

        public override HotkeyId Id => HotkeyId.CameraFollowUnit;

        void Awake()
        {
            cameraTarget = cameraTargetBehaviour as IHotkeyCameraTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyCameraTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            cameraTarget?.OnHotkeyFollowSelected(in context);
        }
    }
}
