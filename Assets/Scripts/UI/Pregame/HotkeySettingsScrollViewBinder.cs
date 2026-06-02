using System;
using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Hotkeys;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Đổ danh sách hotkey vào ScrollView Content bằng prefab HotKeySetting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotkeySettingsScrollViewBinder : MonoBehaviour
    {
        struct RuntimeEntry
        {
            public HotkeyId id;
            public int chordIndex;
            public HotkeyChord persistedChord;
            public HotkeyChord stagedChord;
            public HotkeySettingItemView view;
        }

        [SerializeField] Transform contentRoot;
        [SerializeField] GameObject hotkeySettingPrefab;
        [SerializeField] HotkeyBindingCatalogSO bindingCatalog;
        [SerializeField] bool clearExistingItemsOnBuild = true;
        [SerializeField] bool rebuildOnEnable = true;
        [SerializeField, Min(0.1f)] float doubleClickThreshold = 0.35f;

        readonly List<GameObject> runtimeItems = new();
        readonly List<UnityAction> itemClickActions = new();
        readonly List<RuntimeEntry> entries = new();
        int selectedIndex = -1;
        int lastClickedIndex = -1;
        float lastClickedTime = -1f;

        public event Action StateChanged;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            if (rebuildOnEnable)
            {
                BuildList();
            }
        }

        void Update()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return;
            }

            if (!TryGetPressedChord(out HotkeyChord chord))
            {
                return;
            }

            ApplyCandidateChord(chord);
        }

        /// <summary>
        /// Mục tiêu: Tạo toàn bộ item hotkey trong content.
        /// Cách hoạt động: lấy bindings từ catalog/default, instantiate prefab và set text cho từng dòng.
        /// </summary>
        public void BuildList()
        {
            ResolveReferences();
            if (contentRoot == null || hotkeySettingPrefab == null)
            {
                Debug.LogWarning($"{nameof(HotkeySettingsScrollViewBinder)}: thiếu Content hoặc Prefab.", this);
                return;
            }

            ClearOldItemsIfNeeded();

            IReadOnlyList<HotkeyBindingEntry> bindings = ResolveBindingsWithOverrides();
            for (int i = 0; i < bindings.Count; i++)
            {
                HotkeyBindingEntry entry = bindings[i];
                if (entry.id == HotkeyId.None || entry.chords == null || entry.chords.Length == 0)
                {
                    continue;
                }

                GameObject instance = Instantiate(hotkeySettingPrefab, contentRoot);
                instance.name = $"HotkeyItem_{entry.id}";

                HotkeySettingItemView view = instance.GetComponent<HotkeySettingItemView>();
                if (view == null)
                {
                    view = instance.AddComponent<HotkeySettingItemView>();
                }

                for (int chordIndex = 0; chordIndex < entry.chords.Length; chordIndex++)
                {
                    HotkeyChord chord = entry.chords[chordIndex];
                    GameObject rowInstance = chordIndex == 0 ? instance : Instantiate(hotkeySettingPrefab, contentRoot);
                    if (chordIndex > 0)
                    {
                        rowInstance.name = $"HotkeyItem_{entry.id}_{chordIndex}";
                    }

                    HotkeySettingItemView rowView = chordIndex == 0 ? view : rowInstance.GetComponent<HotkeySettingItemView>();
                    if (rowView == null)
                    {
                        rowView = rowInstance.AddComponent<HotkeySettingItemView>();
                    }

                    rowView.SetData(BuildActionDescription(entry.id, chord), BuildKeyString(chord));
                    rowView.SetVisualState(HotkeySettingItemView.ItemVisualState.Normal);

                    int index = entries.Count;
                    UnityAction clickAction = () => OnItemClicked(index);
                    rowView.RegisterClickListener(clickAction);

                    itemClickActions.Add(clickAction);
                    entries.Add(new RuntimeEntry
                    {
                        id = entry.id,
                        chordIndex = chordIndex,
                        persistedChord = chord,
                        stagedChord = chord,
                        view = rowView
                    });
                    runtimeItems.Add(rowInstance);
                }
            }

            selectedIndex = -1;
            lastClickedIndex = -1;
            lastClickedTime = -1f;
            Debug.Log($"[{nameof(HotkeySettingsScrollViewBinder)}] Built {entries.Count} rows from {bindings.Count} binding entries.", this);
            NotifyStateChanged();
        }

        /// <summary>
        /// Mục tiêu: Trạng thái dirty cho nút Save ở Settings dialog.
        /// Cách hoạt động: so staged chord với persisted chord của từng dòng.
        /// </summary>
        public bool HasPendingChanges()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!AreChordsEqual(entries[i].stagedChord, entries[i].persistedChord))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasConflicts()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (IsChordConflict(i, entries[i].stagedChord))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Commit toàn bộ hotkey staging khi bấm Save.
        /// Cách hoạt động: ghi PlayerPrefs overrides và cập nhật persisted baseline.
        /// </summary>
        public void SaveStagedChanges()
        {
            if (HasConflicts())
            {
                return;
            }

            List<HotkeyBindingEntry> toSave = new(entries.Count);
            Dictionary<HotkeyId, List<(int chordIndex, HotkeyChord chord)>> grouped = new();
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeEntry runtime = entries[i];
                runtime.persistedChord = runtime.stagedChord;
                entries[i] = runtime;

                if (!grouped.TryGetValue(runtime.id, out List<(int chordIndex, HotkeyChord chord)> list))
                {
                    list = new List<(int chordIndex, HotkeyChord chord)>(2);
                    grouped[runtime.id] = list;
                }

                list.Add((runtime.chordIndex, runtime.stagedChord));
            }

            foreach (KeyValuePair<HotkeyId, List<(int chordIndex, HotkeyChord chord)>> pair in grouped)
            {
                pair.Value.Sort((a, b) => a.chordIndex.CompareTo(b.chordIndex));
                HotkeyChord[] chords = new HotkeyChord[pair.Value.Count];
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    chords[i] = pair.Value[i].chord;
                }

                toSave.Add(new HotkeyBindingEntry
                {
                    id = pair.Key,
                    chords = chords
                });
            }

            HotkeyBindingPreferences.SaveOverrides(toSave);
            RefreshAllItemVisuals();
            NotifyStateChanged();
        }

        /// <summary>
        /// Mục tiêu: Cancel/Close settings thì rollback hotkey staging chưa save.
        /// Cách hoạt động: trả staged về persisted và refresh lại UI text/màu.
        /// </summary>
        public void DiscardStagedChanges()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeEntry runtime = entries[i];
                runtime.stagedChord = runtime.persistedChord;
                entries[i] = runtime;
            }

            selectedIndex = -1;
            RefreshAllItemVisuals();
            NotifyStateChanged();
        }

        public void ResetToDefaultsAndSave()
        {
            IReadOnlyList<HotkeyBindingEntry> defaults = HotkeyDefaults.CreateBindings();
            Dictionary<HotkeyId, HotkeyChord> defaultMap = new(defaults.Count);
            for (int i = 0; i < defaults.Count; i++)
            {
                HotkeyBindingEntry entry = defaults[i];
                if (entry.id == HotkeyId.None || entry.chords == null || entry.chords.Length == 0)
                {
                    continue;
                }

                defaultMap[entry.id] = entry.chords[0];
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeEntry runtime = entries[i];
                if (defaultMap.TryGetValue(runtime.id, out HotkeyChord chord))
                {
                    runtime.stagedChord = chord;
                    runtime.persistedChord = chord;
                    entries[i] = runtime;
                }
            }

            HotkeyBindingPreferences.ClearOverrides();
            SaveStagedChanges();
            NotifyStateChanged();
        }

        void ResolveReferences()
        {
            if (contentRoot == null)
            {
                ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null && scrollRect.content != null)
                {
                    contentRoot = scrollRect.content;
                }
                else
                {
                    Transform content = FindDeepChild(transform, "Content");
                    contentRoot = content != null ? content : transform;
                }
            }

            if (hotkeySettingPrefab == null && contentRoot != null && contentRoot.childCount > 0)
            {
                hotkeySettingPrefab = contentRoot.GetChild(0).gameObject;
            }
        }

        void ClearOldItemsIfNeeded()
        {
            if (!clearExistingItemsOnBuild)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].view != null && i < itemClickActions.Count && itemClickActions[i] != null)
                {
                    entries[i].view.UnregisterClickListener(itemClickActions[i]);
                }
            }

            entries.Clear();
            itemClickActions.Clear();

            for (int i = runtimeItems.Count - 1; i >= 0; i--)
            {
                if (runtimeItems[i] != null)
                {
                    Destroy(runtimeItems[i]);
                }
            }

            runtimeItems.Clear();

            // Chỉ xoá item runtime do script tạo, không đụng template có sẵn trong Content.
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child != null && child.name.StartsWith("HotkeyItem_", StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        IReadOnlyList<HotkeyBindingEntry> ResolveBindingsWithOverrides()
        {
            IReadOnlyList<HotkeyBindingEntry> source;
            if (bindingCatalog != null && bindingCatalog.Bindings.Count > 0)
            {
                source = bindingCatalog.Bindings;
            }
            else
            {
                source = HotkeyDefaults.CreateBindings();
            }

            return HotkeyBindingPreferences.ApplyOverrides(source);
        }

        void OnItemClicked(int index)
        {
            float now = Time.unscaledTime;
            bool isDoubleClick = index == lastClickedIndex && now - lastClickedTime <= doubleClickThreshold;
            lastClickedIndex = index;
            lastClickedTime = now;

            if (!isDoubleClick)
            {
                return;
            }

            selectedIndex = index;
            RefreshAllItemVisuals();
        }

        void ApplyCandidateChord(HotkeyChord chord)
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return;
            }

            RuntimeEntry runtime = entries[selectedIndex];
            runtime.stagedChord = chord;
            entries[selectedIndex] = runtime;
            RefreshAllItemVisuals();
            NotifyStateChanged();
        }

        void RefreshAllItemVisuals()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                RuntimeEntry runtime = entries[i];
                if (runtime.view == null)
                {
                    continue;
                }

                runtime.view.SetHotkeyText(BuildKeyString(runtime.stagedChord));

                bool isConflict = IsChordConflict(i, runtime.stagedChord);
                bool isPendingChange = !AreChordsEqual(runtime.stagedChord, runtime.persistedChord);
                bool isSelected = i == selectedIndex;

                if (isConflict)
                {
                    runtime.view.SetVisualState(HotkeySettingItemView.ItemVisualState.Conflict);
                }
                else
                {
                    runtime.view.SetVisualState(
                        (isSelected || isPendingChange)
                            ? HotkeySettingItemView.ItemVisualState.Selected
                            : HotkeySettingItemView.ItemVisualState.Normal);
                }
            }
        }

        bool IsChordConflict(int currentIndex, HotkeyChord candidate)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == currentIndex)
                {
                    continue;
                }

                if (AreChordsEqual(entries[i].stagedChord, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        static bool AreChordsEqual(HotkeyChord a, HotkeyChord b)
        {
            return a.key == b.key && a.shift == b.shift && a.ctrl == b.ctrl && a.alt == b.alt;
        }

        static bool TryGetPressedChord(out HotkeyChord chord)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                chord = default;
                return false;
            }

            var allKeys = keyboard.allKeys;
            for (int i = 0; i < allKeys.Count; i++)
            {
                if (!allKeys[i].wasPressedThisFrame)
                {
                    continue;
                }

                Key pressed = allKeys[i].keyCode;
                if (pressed is Key.LeftCtrl or Key.RightCtrl
                    or Key.LeftShift or Key.RightShift
                    or Key.LeftAlt or Key.RightAlt)
                {
                    continue;
                }

                bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
                chord = new HotkeyChord(pressed, shift, ctrl, alt);
                return true;
            }

            chord = default;
            return false;
        }

        void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        static string BuildActionDescription(HotkeyId id, HotkeyChord chord)
        {
            return id switch
            {
                HotkeyId.CancelSelection => "Hủy chọn / hủy lệnh đang chờ",
                HotkeyId.StopUnits => "Dừng unit đang chọn",
                HotkeyId.DeleteSelection => "Xóa đối tượng đang chọn",
                HotkeyId.DeleteSelectionImmediate => "Xóa đối tượng ngay lập tức",
                HotkeyId.SelectWorkerOnScreen => chord.ctrl ? "Chọn tất cả Worker trên màn hình" : "Chọn một Worker trên màn hình",
                HotkeyId.SelectWarriorOnScreen => chord.ctrl ? "Chọn tất cả Warrior trên màn hình" : "Chọn một Warrior trên màn hình",
                HotkeyId.SelectArcherOnScreen => chord.ctrl ? "Chọn tất cả Archer trên màn hình" : "Chọn một Archer trên màn hình",
                HotkeyId.SelectRockWarriorOnScreen => chord.ctrl ? "Chọn tất cả RockWarrior trên màn hình" : "Chọn một RockWarrior trên màn hình",
                HotkeyId.SelectCivilCentralOnScreen => chord.ctrl ? "Chọn tất cả Civil Central trên màn hình" : "Chọn một Civil Central trên màn hình",
                HotkeyId.SelectForgeOnScreen => chord.ctrl ? "Chọn tất cả Forge trên màn hình" : "Chọn một Forge trên màn hình",
                HotkeyId.SelectBarracksOnScreen => chord.ctrl ? "Chọn tất cả Barracks trên màn hình" : "Chọn một Barracks trên màn hình",
                HotkeyId.ActionBarSlot => $"Dùng lệnh Action Bar ô {ToReadableKey(chord.key.ToString())}",
                HotkeyId.CameraReset => "Đặt lại camera",
                HotkeyId.CameraFollowUnit => "Camera theo unit đang chọn",
                _ => ToReadableName(id.ToString())
            };
        }

        static string BuildKeyString(HotkeyChord[] chords)
        {
            if (chords == null || chords.Length == 0)
            {
                return "-";
            }

            StringBuilder builder = new(32);
            for (int i = 0; i < chords.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append(ToReadableChord(chords[i]));
            }

            return builder.ToString();
        }

        static string BuildKeyString(HotkeyChord chord)
        {
            return ToReadableChord(chord);
        }

        static string ToReadableChord(HotkeyChord chord)
        {
            List<string> parts = new(4);
            if (chord.ctrl) parts.Add("Ctrl");
            if (chord.shift) parts.Add("Shift");
            if (chord.alt) parts.Add("Alt");
            parts.Add(ToReadableKey(chord.key.ToString()));
            return string.Join(" + ", parts);
        }

        static string ToReadableKey(string raw)
        {
            return raw switch
            {
                "Digit1" => "1",
                "Digit2" => "2",
                "Digit3" => "3",
                "Digit4" => "4",
                "Digit5" => "5",
                "Digit6" => "6",
                "Digit7" => "7",
                "Digit8" => "8",
                "Digit9" => "9",
                "Escape" => "Esc",
                "Home" => "Home",
                _ => raw
            };
        }

        static string ToReadableName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            StringBuilder builder = new(raw.Length + 8);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(raw[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
