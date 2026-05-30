namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Định danh phím tắt. Thêm giá trị mới ở cuối enum khi mở rộng tính năng.
    /// Tham chiếu mô tả người chơi: Resources/UI/hotkeys_uts_vi.json
    /// </summary>
    public enum HotkeyId
    {
        None = 0,

        // Session / selection
        CancelSelection,
        StopUnits,
        DeleteSelection,
        DeleteSelectionImmediate,

        /// <summary>Q — một Worker trên màn hình; Ctrl+Q — mọi Worker trên màn hình.</summary>
        SelectWorkerOnScreen,
        /// <summary>W — một Warrior; Ctrl+W — mọi Warrior trên màn hình.</summary>
        SelectWarriorOnScreen,
        /// <summary>E — một Archer; Ctrl+E — mọi Archer trên màn hình.</summary>
        SelectArcherOnScreen,
        /// <summary>R — một RockWarrior; Ctrl+R — mọi RockWarrior trên màn hình.</summary>
        SelectRockWarriorOnScreen,

        /// <summary>A — một Civil Central; Ctrl+A — mọi CC trên màn hình.</summary>
        SelectCivilCentralOnScreen,
        /// <summary>S — một Forge; Ctrl+S — mọi Forge trên màn hình.</summary>
        SelectForgeOnScreen,
        /// <summary>D — một Barracks; Ctrl+D — mọi Barracks trên màn hình.</summary>
        SelectBarracksOnScreen,

        /// <summary>Phím 1–9 — lệnh slot 0–8 trên thanh action (cùng thứ tự UI).</summary>
        ActionBarSlot,

        // Camera (ví dụ mở rộng sau)
        CameraReset,
        CameraFollowUnit,
    }
}
