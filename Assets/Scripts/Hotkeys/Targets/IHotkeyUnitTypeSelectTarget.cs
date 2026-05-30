using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Bridge hotkey Q/W/E/R → chọn unit theo prefab archetype trên màn hình.
    /// </summary>
    public interface IHotkeyUnitTypeSelectTarget
    {
        /// <param name="selectAllOnScreen">True = mọi unit cùng loại trên màn hình; false = một unit (cycle).</param>
        void OnHotkeySelectUnitType(GameObject referencePrefab, bool selectAllOnScreen);
    }
}
