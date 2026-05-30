using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Đọc phím qua Input System. Keyboard được cache một lần mỗi frame trong Tick.
    /// </summary>
    public sealed class UnityHotkeyInputSource : IHotkeyInputSource
    {
        Keyboard keyboard;

        public bool IsAvailable => keyboard != null;

        /// <summary>
        /// Mục tiêu: Cập nhật reference bàn phím trước khi quét binding.
        /// Cách hoạt động: Gán Keyboard.current (null nếu không có thiết bị).
        /// </summary>
        public void Refresh()
        {
            keyboard = Keyboard.current;
        }

        public bool WasChordPressedThisFrame(HotkeyChord chord)
        {
            if (keyboard == null || chord.key == Key.None)
            {
                return false;
            }

            if (!keyboard[chord.key].wasPressedThisFrame)
            {
                return false;
            }

            return keyboard.shiftKey.isPressed == chord.shift
                && keyboard.ctrlKey.isPressed == chord.ctrl
                && keyboard.altKey.isPressed == chord.alt;
        }
    }
}
