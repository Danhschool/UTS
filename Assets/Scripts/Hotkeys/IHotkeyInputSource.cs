namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Đọc trạng thái bàn phím (DIP: HotkeyService không phụ thuộc Unity Input trực tiếp).
    /// </summary>
    public interface IHotkeyInputSource
    {
        bool IsAvailable { get; }
        void Refresh();
        bool WasChordPressedThisFrame(HotkeyChord chord);
    }
}
