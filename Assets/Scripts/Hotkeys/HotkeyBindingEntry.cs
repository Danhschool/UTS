using System;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Một hành động có thể gán nhiều tổ hợp phím thay thế (OR).
    /// </summary>
    [Serializable]
    public struct HotkeyBindingEntry
    {
        public HotkeyId id;
        [Tooltip("Bất kỳ chord nào khớp sẽ kích hoạt hành động.")]
        public HotkeyChord[] chords;
    }
}
