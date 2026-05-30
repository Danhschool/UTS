using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Mapping mặc định UTS (QWERTY). Dùng khi chưa gán HotkeyBindingCatalogSO.
    /// </summary>
    public static class HotkeyDefaults
    {
        public static IReadOnlyList<HotkeyBindingEntry> CreateBindings()
        {
            return new[]
            {
                Entry(HotkeyId.CancelSelection,
                    Chord(Key.Escape)),

                Entry(HotkeyId.StopUnits,
                    Chord(Key.H)),

                Entry(HotkeyId.SelectWorkerOnScreen,
                    Chord(Key.Q),
                    Chord(Key.Q, ctrl: true)),

                Entry(HotkeyId.SelectWarriorOnScreen,
                    Chord(Key.W),
                    Chord(Key.W, ctrl: true)),

                Entry(HotkeyId.SelectArcherOnScreen,
                    Chord(Key.E),
                    Chord(Key.E, ctrl: true)),

                Entry(HotkeyId.SelectRockWarriorOnScreen,
                    Chord(Key.R),
                    Chord(Key.R, ctrl: true)),

                Entry(HotkeyId.SelectCivilCentralOnScreen,
                    Chord(Key.A),
                    Chord(Key.A, ctrl: true)),

                Entry(HotkeyId.SelectForgeOnScreen,
                    Chord(Key.S),
                    Chord(Key.S, ctrl: true)),

                Entry(HotkeyId.SelectBarracksOnScreen,
                    Chord(Key.D),
                    Chord(Key.D, ctrl: true)),

                Entry(HotkeyId.ActionBarSlot,
                    Chord(Key.Digit1),
                    Chord(Key.Digit2),
                    Chord(Key.Digit3),
                    Chord(Key.Digit4),
                    Chord(Key.Digit5),
                    Chord(Key.Digit6),
                    Chord(Key.Digit7),
                    Chord(Key.Digit8),
                    Chord(Key.Digit9)),

                Entry(HotkeyId.DeleteSelection,
                    Chord(Key.Delete)),

                Entry(HotkeyId.DeleteSelectionImmediate,
                    Chord(Key.Delete, shift: true)),

                Entry(HotkeyId.CameraReset,
                    Chord(Key.Home)),

                Entry(HotkeyId.CameraFollowUnit,
                    Chord(Key.F)),
            };
        }

        static HotkeyBindingEntry Entry(HotkeyId id, params HotkeyChord[] chords)
        {
            return new HotkeyBindingEntry { id = id, chords = chords };
        }

        static HotkeyChord Chord(Key key, bool shift = false, bool ctrl = false, bool alt = false)
        {
            return new HotkeyChord(key, shift, ctrl, alt);
        }
    }
}
