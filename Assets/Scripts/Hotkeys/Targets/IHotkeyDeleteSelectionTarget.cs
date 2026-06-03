namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Bridge hotkey Delete / Shift+Delete → xóa selection phe local.
    /// </summary>
    public interface IHotkeyDeleteSelectionTarget
    {
        void OnHotkeyDeleteSelection(in HotkeyContext context, bool immediate);
    }
}
