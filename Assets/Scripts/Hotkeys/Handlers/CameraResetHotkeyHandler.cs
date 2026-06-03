using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Home → reset zoom / góc camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraResetHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour cameraTargetBehaviour;

        IHotkeyCameraTarget cameraTarget;

        public override HotkeyId Id => HotkeyId.CameraReset;

        void Awake()
        {
            cameraTarget = cameraTargetBehaviour as IHotkeyCameraTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyCameraTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            cameraTarget?.OnHotkeyResetCamera(in context);
        }
    }
}
