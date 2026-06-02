using System;
using System.Collections.Generic;
using GameDevTV.RTS.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Quản lý dialog cài đặt MainMenu (tab Audio/Hotkey, tên người chơi, Save/Reset theo cơ chế staging).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuSettingsDialogController : MonoBehaviour
    {
        const string PlayerNamePrefKey = "RTS.Settings.PlayerName";

        enum SettingsTab
        {
            None = 0,
            Audio = 1,
            Hotkey = 2
        }

        [Serializable]
        struct SliderBinding
        {
            public string rowName;
            public AudioVolumeChannel channel;
            public Slider slider;
        }

        [Header("Root")]
        [SerializeField] Transform searchRoot;

        [Header("Tab buttons")]
        [SerializeField] Button buttonAudioTab;
        [SerializeField] Button buttonHotkeyTab;
        [SerializeField] GameDevTV.RTS.UI.Components.HoverRevealPanel audioTabHoverReveal;
        [SerializeField] GameDevTV.RTS.UI.Components.HoverRevealPanel hotkeyTabHoverReveal;

        [Header("Action buttons")]
        [SerializeField] Button buttonSave;
        [SerializeField] Button buttonReset;
        [SerializeField] GameDevTV.RTS.UI.Components.HoverRevealPanel saveButtonHoverReveal;
        [SerializeField] GameDevTV.RTS.UI.Components.HoverRevealPanel resetButtonHoverReveal;

        [Header("Panels")]
        [SerializeField] GameObject panelAudio;
        [SerializeField] GameObject panelHotkey;
        [SerializeField] HotkeySettingsScrollViewBinder hotkeySettingsBinder;

        [Header("Player name")]
        [SerializeField] InputField inputPlayerName;

        [Header("Save button visuals")]
        [SerializeField] Image saveButtonImage;
        [SerializeField] Color saveEnabledColor = Color.white;
        [SerializeField] Color saveDisabledColor = Color.black;

        [Header("Audio sliders")]
        [SerializeField] SliderBinding[] sliderBindings =
        {
            new() { rowName = "Master", channel = AudioVolumeChannel.Master },
            new() { rowName = "Music", channel = AudioVolumeChannel.Music },
            new() { rowName = "Sfx", channel = AudioVolumeChannel.Sfx },
            new() { rowName = "UI", channel = AudioVolumeChannel.Ui },
            new() { rowName = "Voice", channel = AudioVolumeChannel.Voice }
        };

        readonly Dictionary<AudioVolumeChannel, float> stagedVolumes = new();
        readonly Dictionary<AudioVolumeChannel, float> persistedVolumes = new();
        readonly List<Slider> boundSliders = new();
        readonly List<UnityAction<float>> boundSliderActions = new();
        string stagedPlayerName;
        string persistedPlayerName;
        UnityAction<string> playerNameChangedAction;
        SettingsTab activeTab = SettingsTab.None;
        ColorBlock audioTabDefaultColors;
        ColorBlock hotkeyTabDefaultColors;

        void Awake()
        {
            Transform root = searchRoot != null ? searchRoot : transform;
            searchRoot = root;

            TryResolveUiReferences(root);
            ResolveHoverRevealReferences();
            ResolveSliderReferences(root);
            DisableImmediateAudioBinders();
            EnsureSaveVisualReference();
            CacheTabButtonColors();
        }

        void OnEnable()
        {
            BindUiEvents();
            LoadPersistedValuesIntoStage();
            ApplyStagedValuesToUi();
            if (hotkeySettingsBinder != null)
            {
                hotkeySettingsBinder.StateChanged += RefreshSaveButtonState;
            }
            hotkeySettingsBinder?.BuildList();
            HideAllPanels();
            RefreshSaveButtonState();
        }

        void OnDisable()
        {
            RollbackPreviewIfUnsaved();
            hotkeySettingsBinder?.DiscardStagedChanges();
            if (hotkeySettingsBinder != null)
            {
                hotkeySettingsBinder.StateChanged -= RefreshSaveButtonState;
            }
            UnbindUiEvents();
        }

        void LateUpdate()
        {
            // Một số script hover reset màu sau click; ép lại tab active ở cuối frame.
            if (activeTab != SettingsTab.None)
            {
                ApplyTabButtonHoverState();
            }
        }

        /// <summary>
        /// Mục tiêu: Nạp cấu hình đang lưu vào vùng staging mỗi khi mở dialog.
        /// Cách hoạt động: Đọc volume từ AudioVolumeController/AudioAccess và tên từ PlayerPrefs, không apply ra game.
        /// </summary>
        void LoadPersistedValuesIntoStage()
        {
            stagedVolumes.Clear();
            persistedVolumes.Clear();
            for (int i = 0; i < sliderBindings.Length; i++)
            {
                AudioVolumeChannel channel = sliderBindings[i].channel;
                float value = ReadCurrentVolume(channel);
                stagedVolumes[channel] = value;
                persistedVolumes[channel] = value;
            }

            persistedPlayerName = ReadPersistedPlayerName();
            stagedPlayerName = persistedPlayerName;
        }

        /// <summary>
        /// Mục tiêu: Chỉ cập nhật UI hiển thị từ giá trị staging.
        /// Cách hoạt động: SetValueWithoutNotify cho slider và set text input để tránh trigger thay đổi giả.
        /// </summary>
        void ApplyStagedValuesToUi()
        {
            for (int i = 0; i < sliderBindings.Length; i++)
            {
                Slider slider = sliderBindings[i].slider;
                if (slider == null)
                {
                    continue;
                }

                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;

                if (stagedVolumes.TryGetValue(sliderBindings[i].channel, out float value))
                {
                    slider.SetValueWithoutNotify(value);
                }
            }

            if (inputPlayerName != null)
            {
                inputPlayerName.SetTextWithoutNotify(stagedPlayerName);
            }
        }

        /// <summary>
        /// Mục tiêu: Áp dụng thay đổi staging khi người dùng bấm Save.
        /// Cách hoạt động: Ghi PlayerPrefs tên, gọi SetVolume từng channel để lưu + áp runtime, rồi tắt trạng thái dirty.
        /// </summary>
        void SaveNow()
        {
            if (hotkeySettingsBinder != null && hotkeySettingsBinder.HasConflicts())
            {
                RefreshSaveButtonState();
                return;
            }

            for (int i = 0; i < sliderBindings.Length; i++)
            {
                AudioVolumeChannel channel = sliderBindings[i].channel;
                float value = stagedVolumes.TryGetValue(channel, out float staged) ? staged : ReadCurrentVolume(channel);
                ApplyVolume(channel, value);
                persistedVolumes[channel] = value;
            }

            string nameToSave = NormalizePlayerName(stagedPlayerName);
            PlayerPrefs.SetString(PlayerNamePrefKey, nameToSave);
            PlayerPrefs.Save();

            persistedPlayerName = nameToSave;
            stagedPlayerName = nameToSave;
            hotkeySettingsBinder?.SaveStagedChanges();
            RefreshSaveButtonState();
        }

        /// <summary>
        /// Mục tiêu: Trả về cài đặt mặc định và áp dụng ngay khi bấm Reset.
        /// Cách hoạt động: Set staging về default (bao gồm tên máy), cập nhật UI rồi gọi SaveNow.
        /// </summary>
        void ResetToDefaultsAndSave()
        {
            stagedVolumes[AudioVolumeChannel.Master] = 1f;
            stagedVolumes[AudioVolumeChannel.Music] = 0.7f;
            stagedVolumes[AudioVolumeChannel.Sfx] = 0.85f;
            stagedVolumes[AudioVolumeChannel.Ui] = 0.75f;
            stagedVolumes[AudioVolumeChannel.Voice] = 0.85f;
            stagedPlayerName = GetMachineDefaultName();

            ApplyStagedValuesToUi();
            hotkeySettingsBinder?.ResetToDefaultsAndSave();
            SaveNow();
        }

        void OnSliderValueChanged(AudioVolumeChannel channel, float value)
        {
            float clamped = Mathf.Clamp01(value);
            stagedVolumes[channel] = clamped;
            ApplyPreviewVolume(channel, clamped);
            RefreshSaveButtonState();
        }

        void OnPlayerNameChanged(string value)
        {
            stagedPlayerName = value;
            RefreshSaveButtonState();
        }

        void RefreshSaveButtonState()
        {
            bool hasConflict = hotkeySettingsBinder != null && hotkeySettingsBinder.HasConflicts();
            bool canSave = !hasConflict && (HasPendingChanges() || (hotkeySettingsBinder != null && hotkeySettingsBinder.HasPendingChanges()));
            if (buttonSave != null)
            {
                buttonSave.interactable = canSave;
            }

            if (saveButtonImage != null)
            {
                saveButtonImage.color = canSave ? saveEnabledColor : saveDisabledColor;
            }
        }

        /// <summary>
        /// Mục tiêu: Hành vi Hybrid — đóng/cancel khi chưa Save thì trả âm lượng về trước khi mở dialog.
        /// Cách hoạt động: Nếu có pending changes, apply lại persistedVolumes và reset staged values.
        /// </summary>
        void RollbackPreviewIfUnsaved()
        {
            if (!HasPendingChanges())
            {
                return;
            }

            foreach (KeyValuePair<AudioVolumeChannel, float> pair in persistedVolumes)
            {
                ApplyPreviewVolume(pair.Key, pair.Value);
                stagedVolumes[pair.Key] = pair.Value;
            }

            stagedPlayerName = persistedPlayerName;
        }

        void ShowAudioPanel()
        {
            SetActiveTab(SettingsTab.Audio);
            hotkeyTabHoverReveal?.ClearLock();
        }

        void ShowHotkeyPanel()
        {
            SetActiveTab(SettingsTab.Hotkey);
            audioTabHoverReveal?.ClearLock();
        }

        void HideAllPanels()
        {
            SetActiveTab(SettingsTab.None);
            audioTabHoverReveal?.ClearLock();
            hotkeyTabHoverReveal?.ClearLock();
        }

        void SetActiveTab(SettingsTab tab)
        {
            activeTab = tab;

            if (panelAudio != null && !IsDialogRoot(panelAudio))
            {
                panelAudio.SetActive(tab == SettingsTab.Audio);
            }

            if (panelHotkey != null && !IsDialogRoot(panelHotkey))
            {
                panelHotkey.SetActive(tab == SettingsTab.Hotkey);
            }

            ApplyTabButtonHoverState();
        }

        bool IsDialogRoot(GameObject panel)
        {
            return panel == gameObject || (searchRoot != null && panel == searchRoot.gameObject);
        }

        void CacheTabButtonColors()
        {
            if (buttonAudioTab != null)
            {
                audioTabDefaultColors = buttonAudioTab.colors;
            }

            if (buttonHotkeyTab != null)
            {
                hotkeyTabDefaultColors = buttonHotkeyTab.colors;
            }
        }

        void ApplyTabButtonHoverState()
        {
            ApplyButtonHoverVisual(buttonAudioTab, audioTabDefaultColors, activeTab == SettingsTab.Audio);
            ApplyButtonHoverVisual(buttonHotkeyTab, hotkeyTabDefaultColors, activeTab == SettingsTab.Hotkey);
        }

        static void ApplyButtonHoverVisual(Button button, ColorBlock defaultColors, bool active)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = defaultColors;
            if (active)
            {
                colors.normalColor = defaultColors.highlightedColor;
                colors.selectedColor = defaultColors.highlightedColor;
            }

            button.colors = colors;
        }

        void BindUiEvents()
        {
            Bind(buttonAudioTab, ShowAudioPanel);
            Bind(buttonHotkeyTab, ShowHotkeyPanel);
            Bind(buttonSave, SaveNow);
            Bind(buttonReset, ResetToDefaultsAndSave);

            for (int i = 0; i < sliderBindings.Length; i++)
            {
                Slider slider = sliderBindings[i].slider;
                if (slider == null)
                {
                    continue;
                }

                AudioVolumeChannel channel = sliderBindings[i].channel;
                UnityAction<float> action = value => OnSliderValueChanged(channel, value);
                slider.onValueChanged.AddListener(action);
                boundSliders.Add(slider);
                boundSliderActions.Add(action);
            }

            if (inputPlayerName != null)
            {
                playerNameChangedAction ??= OnPlayerNameChanged;
                inputPlayerName.onValueChanged.AddListener(playerNameChangedAction);
            }
        }

        void UnbindUiEvents()
        {
            Unbind(buttonAudioTab, ShowAudioPanel);
            Unbind(buttonHotkeyTab, ShowHotkeyPanel);
            Unbind(buttonSave, SaveNow);
            Unbind(buttonReset, ResetToDefaultsAndSave);

            for (int i = 0; i < boundSliders.Count; i++)
            {
                if (boundSliders[i] != null && boundSliderActions[i] != null)
                {
                    boundSliders[i].onValueChanged.RemoveListener(boundSliderActions[i]);
                }
            }
            boundSliders.Clear();
            boundSliderActions.Clear();

            if (inputPlayerName != null && playerNameChangedAction != null)
            {
                inputPlayerName.onValueChanged.RemoveListener(playerNameChangedAction);
            }
        }

        void TryResolveUiReferences(Transform root)
        {
            TryResolveButton(root, new[] { "Button Audio", "Btn Audio", "Audio" }, ref buttonAudioTab);
            TryResolveButton(root, new[] { "Button Hotkey", "Button Phim Tat", "Btn Hotkey", "Hotkey" }, ref buttonHotkeyTab);
            TryResolveButton(root, new[] { "Button Save", "Btn Save", "Save" }, ref buttonSave);
            TryResolveButton(root, new[] { "Button Reset", "Btn Reset", "Reset" }, ref buttonReset);

            TryResolvePanel(root, new[] { "Panel Audio", "Audio Panel", "Panel Am Thanh" }, ref panelAudio);
            TryResolvePanel(root, new[] { "Panel Hotkey", "Hotkey Panel", "Panel Phim Tat" }, ref panelHotkey);
            if (hotkeySettingsBinder == null && panelHotkey != null)
            {
                hotkeySettingsBinder = panelHotkey.GetComponentInChildren<HotkeySettingsScrollViewBinder>(true);
            }

            if (inputPlayerName == null)
            {
                Transform input = FindDeepChild(root, "InputField_Name");
                if (input != null)
                {
                    inputPlayerName = input.GetComponent<InputField>();
                }
            }
        }

        void ResolveSliderReferences(Transform root)
        {
            for (int i = 0; i < sliderBindings.Length; i++)
            {
                if (sliderBindings[i].slider != null)
                {
                    continue;
                }

                Transform row = FindDeepChild(root, sliderBindings[i].rowName);
                if (row == null)
                {
                    continue;
                }

                Slider slider = row.GetComponentInChildren<Slider>(true);
                if (slider == null)
                {
                    continue;
                }

                sliderBindings[i].slider = slider;
            }
        }

        void ResolveHoverRevealReferences()
        {
            if (audioTabHoverReveal == null && buttonAudioTab != null)
            {
                audioTabHoverReveal = buttonAudioTab.GetComponent<GameDevTV.RTS.UI.Components.HoverRevealPanel>();
            }

            if (hotkeyTabHoverReveal == null && buttonHotkeyTab != null)
            {
                hotkeyTabHoverReveal = buttonHotkeyTab.GetComponent<GameDevTV.RTS.UI.Components.HoverRevealPanel>();
            }

            if (saveButtonHoverReveal == null && buttonSave != null)
            {
                saveButtonHoverReveal = buttonSave.GetComponent<GameDevTV.RTS.UI.Components.HoverRevealPanel>();
            }

            if (resetButtonHoverReveal == null && buttonReset != null)
            {
                resetButtonHoverReveal = buttonReset.GetComponent<GameDevTV.RTS.UI.Components.HoverRevealPanel>();
            }

            // Chỉ tab mở panel mới giữ hover sau click.
            audioTabHoverReveal?.SetLockRevealAfterClick(true);
            hotkeyTabHoverReveal?.SetLockRevealAfterClick(true);
            saveButtonHoverReveal?.SetLockRevealAfterClick(false);
            resetButtonHoverReveal?.SetLockRevealAfterClick(false);
        }

        void DisableImmediateAudioBinders()
        {
            for (int i = 0; i < sliderBindings.Length; i++)
            {
                Slider slider = sliderBindings[i].slider;
                if (slider == null)
                {
                    continue;
                }

                AudioVolumeSliderBinder binder = slider.GetComponent<AudioVolumeSliderBinder>();
                if (binder != null)
                {
                    binder.enabled = false;
                }
            }
        }

        void EnsureSaveVisualReference()
        {
            if (buttonSave != null)
            {
                // Save chỉ đổi màu theo trạng thái dirty, không bị đổi do hover transition.
                buttonSave.transition = Selectable.Transition.None;
            }

            if (saveButtonImage == null && buttonSave != null)
            {
                saveButtonImage = buttonSave.GetComponent<Image>();
            }

            RefreshSaveButtonState();
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        static void TryResolveButton(Transform root, string[] candidates, ref Button target)
        {
            if (target != null || root == null)
            {
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform found = FindDeepChild(root, candidates[i]);
                if (found == null)
                {
                    continue;
                }

                target = found.GetComponent<Button>();
                if (target != null)
                {
                    return;
                }
            }
        }

        static void TryResolvePanel(Transform root, string[] candidates, ref GameObject target)
        {
            if (target != null || root == null)
            {
                return;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                Transform found = FindDeepChild(root, candidates[i]);
                if (found == null)
                {
                    continue;
                }

                target = found.gameObject;
                return;
            }
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

        static string GetMachineDefaultName()
        {
            string name = System.Environment.MachineName;
            return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        }

        static string NormalizePlayerName(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? GetMachineDefaultName() : raw.Trim();
        }

        static string ReadPersistedPlayerName()
        {
            if (!PlayerPrefs.HasKey(PlayerNamePrefKey))
            {
                return GetMachineDefaultName();
            }

            return NormalizePlayerName(PlayerPrefs.GetString(PlayerNamePrefKey));
        }

        static float ReadCurrentVolume(AudioVolumeChannel channel)
        {
            AudioVolumeController controller = AudioVolumeController.Instance;
            if (controller != null)
            {
                return controller.GetVolume(channel);
            }

            return AudioAccess.GetVolume(channel);
        }

        bool HasPendingChanges()
        {
            if (!string.Equals(NormalizePlayerName(stagedPlayerName), NormalizePlayerName(persistedPlayerName), StringComparison.Ordinal))
            {
                return true;
            }

            foreach (KeyValuePair<AudioVolumeChannel, float> pair in persistedVolumes)
            {
                if (!stagedVolumes.TryGetValue(pair.Key, out float staged))
                {
                    return true;
                }

                if (!Mathf.Approximately(staged, pair.Value))
                {
                    return true;
                }
            }

            return false;
        }

        static void ApplyPreviewVolume(AudioVolumeChannel channel, float value)
        {
            AudioVolumeController controller = AudioVolumeController.Instance;
            if (controller != null)
            {
                controller.PreviewVolume(channel, value);
                return;
            }

            AudioAccess.SetVolume(channel, value);
        }

        static void ApplyVolume(AudioVolumeChannel channel, float value)
        {
            AudioVolumeController controller = AudioVolumeController.Instance;
            if (controller != null)
            {
                controller.SetVolume(channel, value);
                return;
            }

            AudioAccess.SetVolume(channel, value);
        }
    }
}
