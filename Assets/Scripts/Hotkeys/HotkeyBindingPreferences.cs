using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// SRP: Lưu/đọc override hotkey trong PlayerPrefs để runtime và UI dùng chung.
    /// </summary>
    public static class HotkeyBindingPreferences
    {
        const string PrefKey = "RTS.Hotkeys.BindingOverrides";

        [Serializable]
        class OverrideDocument
        {
            public List<OverrideEntry> entries = new();
        }

        [Serializable]
        class OverrideEntry
        {
            public int id;
            public int key;
            public bool shift;
            public bool ctrl;
            public bool alt;
            public int chordIndex;
        }

        public static IReadOnlyList<HotkeyBindingEntry> ApplyOverrides(IReadOnlyList<HotkeyBindingEntry> source)
        {
            List<HotkeyBindingEntry> clone = CloneBindings(source);
            Dictionary<HotkeyId, List<OverrideRecord>> overrides = LoadOverrides();
            if (overrides.Count == 0)
            {
                return clone;
            }

            for (int i = 0; i < clone.Count; i++)
            {
                HotkeyBindingEntry entry = clone[i];
                if (!overrides.TryGetValue(entry.id, out List<OverrideRecord> rows) || rows.Count == 0)
                {
                    continue;
                }

                rows.Sort((a, b) => a.chordIndex.CompareTo(b.chordIndex));
                int requiredLength = entry.chords != null ? entry.chords.Length : 0;
                for (int j = 0; j < rows.Count; j++)
                {
                    int candidateLength = rows[j].chordIndex + 1;
                    if (candidateLength > requiredLength)
                    {
                        requiredLength = candidateLength;
                    }
                }

                if (requiredLength <= 0)
                {
                    continue;
                }

                HotkeyChord[] chords = new HotkeyChord[requiredLength];
                if (entry.chords != null && entry.chords.Length > 0)
                {
                    Array.Copy(entry.chords, chords, Mathf.Min(entry.chords.Length, chords.Length));
                }

                for (int j = 0; j < rows.Count; j++)
                {
                    int index = rows[j].chordIndex;
                    if (index < 0 || index >= chords.Length)
                    {
                        continue;
                    }

                    chords[index] = rows[j].chord;
                }

                entry.chords = chords;
                clone[i] = entry;
            }

            return clone;
        }

        struct OverrideRecord
        {
            public int chordIndex;
            public HotkeyChord chord;
        }

        /// <summary>
        /// Mục tiêu: Lưu override phím người chơi sau khi bấm Save trong settings.
        /// Cách hoạt động: Serialize JSON vào PlayerPrefs theo id→chord (first chord).
        /// </summary>
        public static void SaveOverrides(IReadOnlyList<HotkeyBindingEntry> bindings)
        {
            OverrideDocument document = new();
            if (bindings != null)
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    HotkeyBindingEntry entry = bindings[i];
                    if (entry.id == HotkeyId.None || entry.chords == null || entry.chords.Length == 0)
                    {
                        continue;
                    }

                    for (int chordIndex = 0; chordIndex < entry.chords.Length; chordIndex++)
                    {
                        HotkeyChord chord = entry.chords[chordIndex];
                        document.entries.Add(new OverrideEntry
                        {
                            id = (int)entry.id,
                            key = (int)chord.key,
                            shift = chord.shift,
                            ctrl = chord.ctrl,
                            alt = chord.alt,
                            chordIndex = chordIndex
                        });
                    }
                }
            }

            string json = JsonUtility.ToJson(document);
            PlayerPrefs.SetString(PrefKey, json);
            PlayerPrefs.Save();
        }

        public static void ClearOverrides()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        static Dictionary<HotkeyId, List<OverrideRecord>> LoadOverrides()
        {
            Dictionary<HotkeyId, List<OverrideRecord>> result = new();
            if (!PlayerPrefs.HasKey(PrefKey))
            {
                return result;
            }

            string json = PlayerPrefs.GetString(PrefKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            OverrideDocument document = JsonUtility.FromJson<OverrideDocument>(json);
            if (document?.entries == null)
            {
                return result;
            }

            for (int i = 0; i < document.entries.Count; i++)
            {
                OverrideEntry entry = document.entries[i];
                HotkeyId id = (HotkeyId)entry.id;
                Key key = (Key)entry.key;
                if (id == HotkeyId.None || key == Key.None)
                {
                    continue;
                }

                if (!result.TryGetValue(id, out List<OverrideRecord> rows))
                {
                    rows = new List<OverrideRecord>(2);
                    result[id] = rows;
                }

                rows.Add(new OverrideRecord
                {
                    chordIndex = entry.chordIndex,
                    chord = new HotkeyChord(key, entry.shift, entry.ctrl, entry.alt)
                });
            }

            return result;
        }

        static List<HotkeyBindingEntry> CloneBindings(IReadOnlyList<HotkeyBindingEntry> source)
        {
            List<HotkeyBindingEntry> clone = new(source?.Count ?? 0);
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                HotkeyBindingEntry entry = source[i];
                HotkeyChord[] chords = entry.chords;
                if (chords != null)
                {
                    HotkeyChord[] copied = new HotkeyChord[chords.Length];
                    Array.Copy(chords, copied, chords.Length);
                    entry.chords = copied;
                }

                clone.Add(entry);
            }

            return clone;
        }
    }
}
