using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// H → gọi IHotkeyStopUnitsTarget. Mẫu handler lệnh unit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopUnitsHotkeyHandler : HotkeyHandlerBase
    {
        [SerializeField] MonoBehaviour stopUnitsTargetBehaviour;

        IHotkeyStopUnitsTarget stopUnitsTarget;

        public override HotkeyId Id => HotkeyId.StopUnits;

        void Awake()
        {
            stopUnitsTarget = stopUnitsTargetBehaviour as IHotkeyStopUnitsTarget
                ?? HotkeyComponentUtility.FindInterfaceOnGameObject<IHotkeyStopUnitsTarget>(gameObject);
        }

        public override void Execute(in HotkeyContext context)
        {
            stopUnitsTarget?.OnHotkeyStopUnits(in context);
        }
    }
}
