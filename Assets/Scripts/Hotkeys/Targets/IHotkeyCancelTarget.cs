using GameDevTV.RTS.Hotkeys;

namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Abstraction cho hủy chọn / hủy đặt công trình (Esc). PlayerInput có thể implement sau.
    /// </summary>
    public interface IHotkeyCancelTarget
    {
        void OnHotkeyCancel(in HotkeyContext context);
    }
}
