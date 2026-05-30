namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Một handler xử lý đúng một HotkeyId. Thêm class mới implement interface này để mở rộng.
    /// </summary>
    public interface IHotkeyHandler
    {
        HotkeyId Id { get; }
        void Execute(in HotkeyContext context);
    }
}
