using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Một tổ hợp phím (modifier + phím chính). So khớp theo Input System Key.
    /// </summary>
    [Serializable]
    public struct HotkeyChord
    {
        public Key key;
        public bool shift;
        public bool ctrl;
        public bool alt;

        public HotkeyChord(Key key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            this.key = key;
            this.shift = shift;
            this.ctrl = ctrl;
            this.alt = alt;
        }

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>(4);
            if (ctrl) parts.Add("Ctrl");
            if (shift) parts.Add("Shift");
            if (alt) parts.Add("Alt");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }
}
