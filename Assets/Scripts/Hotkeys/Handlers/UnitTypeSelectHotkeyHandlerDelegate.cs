using GameDevTV.RTS.Hotkeys.Targets;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// SRP: Handler không-MonoBehaviour — một cặp HotkeyId + prefab unit.
    /// </summary>
    public sealed class UnitTypeSelectHotkeyHandlerDelegate : IHotkeyHandler
    {
        readonly HotkeyId id;
        readonly GameObject referencePrefab;
        readonly IHotkeyUnitTypeSelectTarget target;

        public UnitTypeSelectHotkeyHandlerDelegate(
            HotkeyId id,
            GameObject referencePrefab,
            IHotkeyUnitTypeSelectTarget target)
        {
            this.id = id;
            this.referencePrefab = referencePrefab;
            this.target = target;
        }

        public HotkeyId Id => id;

        /// <summary>
        /// Mục tiêu: Phím Q/W/E/R, A/S/D hoặc Ctrl+tương ứng chọn unit/nhà theo loại.
        /// Cách hoạt động: Ctrl trong chord → chọn tất cả trên màn hình; không Ctrl → chọn một (cycle).
        /// </summary>
        public void Execute(in HotkeyContext context)
        {
            if (target == null || referencePrefab == null)
            {
                return;
            }

            bool selectAll = context.MatchedChord.ctrl;
            target.OnHotkeySelectUnitType(referencePrefab, selectAll);
        }
    }
}
