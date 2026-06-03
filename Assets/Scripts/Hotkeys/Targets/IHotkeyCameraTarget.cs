namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Bridge hotkey Home / F → camera gameplay.
    /// </summary>
    public interface IHotkeyCameraTarget
    {
        void OnHotkeyResetCamera(in HotkeyContext context);

        void OnHotkeyFollowSelected(in HotkeyContext context);
    }
}
