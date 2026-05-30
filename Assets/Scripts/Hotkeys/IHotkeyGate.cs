namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Chặn hotkey khi đang nhập chat/UI (ISP: gate nhỏ, một nhiệm vụ).
    /// </summary>
    public interface IHotkeyGate
    {
        bool IsBlocked(in HotkeyContext context);
    }
}
