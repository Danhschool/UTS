namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Ngữ cảnh khi một phím tắt được kích hoạt (mở rộng thêm field khi cần).
    /// </summary>
    public readonly struct HotkeyContext
    {
        public HotkeyId Id { get; }
        public HotkeyChord MatchedChord { get; }

        public HotkeyContext(HotkeyId id, HotkeyChord matchedChord)
        {
            Id = id;
            MatchedChord = matchedChord;
        }
    }
}
