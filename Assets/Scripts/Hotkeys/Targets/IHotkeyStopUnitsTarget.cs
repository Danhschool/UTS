using GameDevTV.RTS.Hotkeys;

namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Abstraction cho lệnh Stop (H). Unit selection controller implement sau.
    /// </summary>
    public interface IHotkeyStopUnitsTarget
    {
        void OnHotkeyStopUnits(in HotkeyContext context);
    }
}
