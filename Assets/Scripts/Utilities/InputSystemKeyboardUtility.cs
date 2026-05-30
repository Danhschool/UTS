using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Truy cập bàn phím qua Input System (không dùng UnityEngine.Input / KeyCode runtime).
    /// </summary>
    public static class InputSystemKeyboardUtility
    {
        /// <summary>
        /// Mục tiêu: Bấm phím một lần trong frame hiện tại, an toàn khi Key serialize sai (đổi từ KeyCode cũ).
        /// Cách hoạt động: null keyboard → false; Key không hợp lệ → false; ngược lại đọc KeyControl.
        /// </summary>
        public static bool WasPressedThisFrame(Key key)
        {
            if (!TryGetKeyControl(key, out KeyControl control))
            {
                return false;
            }

            return control.wasPressedThisFrame;
        }

        /// <summary>
        /// Mục tiêu: Phím đang được giữ.
        /// Cách hoạt động: Giống <see cref="WasPressedThisFrame"/> với isPressed.
        /// </summary>
        public static bool IsPressed(Key key)
        {
            if (!TryGetKeyControl(key, out KeyControl control))
            {
                return false;
            }

            return control.isPressed;
        }

        /// <summary>
        /// Mục tiêu: Chuẩn hóa Key sau khi đổi field từ KeyCode (YAML còn số KeyCode cũ trên prefab).
        /// Cách hoạt động: Thử map KeyCode → Key; không được thì giữ key nếu hợp lệ; cuối cùng fallback.
        /// </summary>
        public static Key CoerceKeyboardKey(Key serializedKey, Key fallback)
        {
            if (TryGetKeyControl(serializedKey, out _))
            {
                return serializedKey;
            }

            int raw = (int)serializedKey;
            if (Enum.IsDefined(typeof(KeyCode), raw)
                && TryMapKeyCodeToKey((KeyCode)raw, out Key fromKeyCode))
            {
                return fromKeyCode;
            }

            return fallback;
        }

        static bool TryGetKeyControl(Key key, out KeyControl control)
        {
            control = null;
            if (key == Key.None)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            try
            {
                control = keyboard[key];
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            return control != null;
        }

        /// <summary>
        /// Mục tiêu: Chuyển KeyCode (Inspector cũ) sang Key Input System.
        /// Cách hoạt động: Bảng map tường minh cho phím thường dùng trong project.
        /// </summary>
        public static bool TryMapKeyCodeToKey(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.F1: key = Key.F1; return true;
                case KeyCode.F2: key = Key.F2; return true;
                case KeyCode.F3: key = Key.F3; return true;
                case KeyCode.F4: key = Key.F4; return true;
                case KeyCode.F5: key = Key.F5; return true;
                case KeyCode.F6: key = Key.F6; return true;
                case KeyCode.F7: key = Key.F7; return true;
                case KeyCode.F8: key = Key.F8; return true;
                case KeyCode.F9: key = Key.F9; return true;
                case KeyCode.F10: key = Key.F10; return true;
                case KeyCode.F11: key = Key.F11; return true;
                case KeyCode.F12: key = Key.F12; return true;
                case KeyCode.V: key = Key.V; return true;
                case KeyCode.B: key = Key.B; return true;
                case KeyCode.Space: key = Key.Space; return true;
                case KeyCode.Escape: key = Key.Escape; return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: key = Key.Enter; return true;
                case KeyCode.Delete: key = Key.Delete; return true;
                case KeyCode.Home: key = Key.Home; return true;
                case KeyCode.A: key = Key.A; return true;
                case KeyCode.S: key = Key.S; return true;
                case KeyCode.D: key = Key.D; return true;
                case KeyCode.Q: key = Key.Q; return true;
                case KeyCode.W: key = Key.W; return true;
                case KeyCode.E: key = Key.E; return true;
                case KeyCode.R: key = Key.R; return true;
                case KeyCode.H: key = Key.H; return true;
                case KeyCode.Alpha1: key = Key.Digit1; return true;
                case KeyCode.Alpha2: key = Key.Digit2; return true;
                case KeyCode.Alpha3: key = Key.Digit3; return true;
                case KeyCode.Alpha4: key = Key.Digit4; return true;
                case KeyCode.Alpha5: key = Key.Digit5; return true;
                case KeyCode.Alpha6: key = Key.Digit6; return true;
                case KeyCode.Alpha7: key = Key.Digit7; return true;
                case KeyCode.Alpha8: key = Key.Digit8; return true;
                case KeyCode.Alpha9: key = Key.Digit9; return true;
                case KeyCode.UpArrow: key = Key.UpArrow; return true;
                case KeyCode.DownArrow: key = Key.DownArrow; return true;
                case KeyCode.LeftArrow: key = Key.LeftArrow; return true;
                case KeyCode.RightArrow: key = Key.RightArrow; return true;
                default:
                    key = Key.None;
                    return false;
            }
        }
    }
}
