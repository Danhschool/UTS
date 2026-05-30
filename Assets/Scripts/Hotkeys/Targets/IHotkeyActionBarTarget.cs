namespace GameDevTV.RTS.Hotkeys.Targets
{
    /// <summary>
    /// Bridge phím 1–9 → lệnh slot trên thanh action của selection hiện tại.
    /// </summary>
    public interface IHotkeyActionBarTarget
    {
        /// <param name="slotIndex">0 = phím 1, …, 8 = phím 9 (khớp <see cref="Commands.BaseCommand.Slot"/>).</param>
        void OnHotkeyActionBarSlot(int slotIndex);
    }
}
