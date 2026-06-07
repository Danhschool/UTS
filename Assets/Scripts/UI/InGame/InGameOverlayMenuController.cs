using System.Collections;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Pregame;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: HUD in-game — nút tốc độ / menu, panel con (speed, menu, pause, settings, đầu hàng).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameOverlayMenuController : MonoBehaviour
    {
        const string SurrenderMessage =
            "Bạn có chắc muốn đầu hàng không?\nTrận sẽ kết thúc với kết quả thua.";

        [Header("Toolbar")]
        [SerializeField] Button buttonGameSpeed;
        [SerializeField] Button buttonGameMenu;

        [Header("Panels")]
        [SerializeField] GameObject panelSpeed;
        [SerializeField] GameObject panelMenu;
        [SerializeField] GameObject panelPause;

        [Header("Speed presets")]
        [SerializeField] UiExclusiveSelectGroup speedSelectGroup;
        [SerializeField] Button buttonSpeedHalf;
        [SerializeField] Button buttonSpeedNormal;
        [SerializeField] Button buttonSpeedFast;
        [SerializeField] Button buttonSpeedDouble;

        [Header("Menu actions")]
        [SerializeField] Button buttonMenuSettings;
        [SerializeField] Button buttonMenuPause;
        [SerializeField] Button buttonMenuSurrender;
        [SerializeField] Button buttonPauseResume;

        [Header("Dialogs")]
        [SerializeField] InGameSettingsPanelOpener settingsOpener;
        [SerializeField] ExitConfirmDialog surrenderConfirmDialog;

        [Header("Match end")]
        [SerializeField] string endSceneName = MatchOutcomeFlow.DefaultEndScene;

        [Header("Optional auto-bind")]
        [SerializeField] Transform searchRoot;

        OverlayPanel _openPanel = OverlayPanel.None;
        bool _surrenderDialogOpen;
        bool _applyingRemoteState;

        static bool IsNetworkMatch => GameMatchOverlayStateSync.IsNetworkMatchActive;

        enum OverlayPanel
        {
            None = 0,
            Speed = 1,
            Menu = 2,
            Pause = 3
        }

        void Awake()
        {
            MatchOutcomeFlow.SetActiveEndScene(endSceneName);

            Transform root = searchRoot != null ? searchRoot : transform;
            TryResolveReferences(root);
            HideAllPanels();
        }

        void OnEnable()
        {
            Bind(buttonGameSpeed, OnGameSpeedToolbarClicked);
            Bind(buttonGameMenu, OnGameMenuToolbarClicked);
            Bind(buttonMenuSettings, OnMenuSettingsClicked);
            Bind(buttonMenuPause, OnMenuPauseClicked);
            Bind(buttonMenuSurrender, OnMenuSurrenderClicked);
            Bind(buttonPauseResume, OnPauseResumeClicked);

            if (settingsOpener != null)
            {
                settingsOpener.Closed += OnSettingsPanelClosed;
            }

            if (speedSelectGroup != null)
            {
                speedSelectGroup.SelectionChanged += OnSpeedPresetSelected;
                speedSelectGroup.RefreshOptions();
                speedSelectGroup.Select(1, notify: false);
            }
        }

        void OnDisable()
        {
            Unbind(buttonGameSpeed, OnGameSpeedToolbarClicked);
            Unbind(buttonGameMenu, OnGameMenuToolbarClicked);
            Unbind(buttonMenuSettings, OnMenuSettingsClicked);
            Unbind(buttonMenuPause, OnMenuPauseClicked);
            Unbind(buttonMenuSurrender, OnMenuSurrenderClicked);
            Unbind(buttonPauseResume, OnPauseResumeClicked);

            if (settingsOpener != null)
            {
                settingsOpener.Closed -= OnSettingsPanelClosed;
            }

            if (speedSelectGroup != null)
            {
                speedSelectGroup.SelectionChanged -= OnSpeedPresetSelected;
            }
        }

        void OnGameSpeedToolbarClicked()
        {
            settingsOpener?.Hide();
            TogglePanel(OverlayPanel.Speed);
        }

        void OnGameMenuToolbarClicked()
        {
            settingsOpener?.Hide();
            TogglePanel(OverlayPanel.Menu);
        }

        void OnMenuSettingsClicked()
        {
            HidePanelsExcept(OverlayPanel.Menu);
            settingsOpener?.Show();
            SyncLocalSettingsPauseOnly();
        }

        void OnSettingsPanelClosed()
        {
            SyncLocalSettingsPauseOnly();
        }

        void OnMenuPauseClicked()
        {
            settingsOpener?.Hide();
            ShowPanel(OverlayPanel.Pause);
            SyncGameplayPause();
        }

        void OnMenuSurrenderClicked()
        {
            if (surrenderConfirmDialog == null)
            {
                Debug.LogWarning($"{nameof(InGameOverlayMenuController)}: chưa gán surrender dialog.", this);
                return;
            }

            _surrenderDialogOpen = true;
            ShowSurrenderConfirmDialogDeferred();

            SyncGameplayPause();
        }

        void OnSurrenderCancelled()
        {
            _surrenderDialogOpen = false;

            SyncGameplayPause();
        }

        /// <summary>
        /// Mục tiêu: Tránh click Surrender rơi xuống nút OK cùng frame (dialog vừa bật).
        /// Cách hoạt động: Hoãn ShowMatchEndConfirm sang frame sau khi EventSystem xử lý xong click Surrender.
        /// </summary>
        void ShowSurrenderConfirmDialogDeferred()
        {
            StartCoroutine(ShowSurrenderConfirmDialogDeferredRoutine());
        }

        IEnumerator ShowSurrenderConfirmDialogDeferredRoutine()
        {
            yield return null;

            if (!_surrenderDialogOpen || surrenderConfirmDialog == null)
            {
                yield break;
            }

            surrenderConfirmDialog.ShowMatchEndConfirm(
                SurrenderMessage,
                OnSurrenderConfirmed,
                OnSurrenderCancelled);
        }

        void OnPauseResumeClicked()
        {
            settingsOpener?.Hide();
            HideAllPanels();
            SyncGameplayPause();
        }

        /// <summary>
        /// Mục tiêu: Chỉ chạy sau khi người chơi bấm OK trên ExitConfirmDialog đầu hàng.
        /// Cách hoạt động: Đóng overlay, rồi load scene End qua MatchOutcomeDetector.
        /// </summary>
        void OnSurrenderConfirmed()
        {
            _surrenderDialogOpen = false;
            HideAllPanels();
            settingsOpener?.Hide();

            if (IsNetworkMatch)
            {
                GameMatchOverlayStateSync.RequestSurrenderConfirmed();
                return;
            }

            ProceedToEndAfterSurrenderConfirmed();
        }

        void ProceedToEndAfterSurrenderConfirmed()
        {
            MatchOutcomeDetector.ShowSurrenderDefeat(endSceneName);
        }

        void OnSpeedPresetSelected(int index)
        {
            GameSpeedController speed = GameSpeedController.Instance;
            if (speed == null)
            {
                Debug.LogWarning("[InGameOverlay] Thiếu GameSpeedController trong scene gameplay.");
                return;
            }

            float selectedScale = speed.GetPresetScale(index);

            if (IsNetworkMatch)
            {
                GameMatchOverlayStateSync.RequestSharedSpeed(index, selectedScale);
                _openPanel = OverlayPanel.None;
                ApplyPanelVisibilityFromRemote(OverlayPanel.None);
                return;
            }

            CloseOverlayPanelsAndResumeGameplay(selectedScale);
        }

        /// <summary>
        /// Mục tiêu: Sau khi chọn tốc độ — đóng panel speed/menu, chạy game với tốc độ mới.
        /// Cách hoạt động: Ẩn mọi panel overlay rồi ResumeAtSpeed (không hiện panel pause).
        /// </summary>
        void CloseOverlayPanelsAndResumeGameplay(float timeScale)
        {
            _surrenderDialogOpen = false;
            HideAllPanels();
            GamePauseService.ResumeAtSpeed(timeScale);
        }

        void TogglePanel(OverlayPanel panel)
        {
            if (IsNetworkMatch)
            {
                OverlayPanel next = IsPanelVisible(panel) ? OverlayPanel.None : panel;
                _openPanel = next;
                settingsOpener?.Hide();
                ApplyPanelVisibilityFromRemote(next);
                PublishSharedOverlayState(next);
                SyncLocalSettingsPauseOnly();
                return;
            }

            if (IsPanelVisible(panel))
            {
                HideAllPanels();
                SyncGameplayPause();
                return;
            }

            ShowPanel(panel);
            SyncGameplayPause();
        }

        void ShowPanel(OverlayPanel panel)
        {
            _openPanel = panel;

            if (panel == OverlayPanel.Speed)
            {
                ActivatePanelDeferred(panelSpeed);
                SetPanelActive(panelMenu, false);
                SetPanelActive(panelPause, false);
                return;
            }

            if (panel == OverlayPanel.Menu)
            {
                ActivatePanelDeferred(panelMenu);
                SetPanelActive(panelSpeed, false);
                SetPanelActive(panelPause, false);
                return;
            }

            if (panel == OverlayPanel.Pause)
            {
                ActivatePanelDeferred(panelPause);
                SetPanelActive(panelSpeed, false);
                SetPanelActive(panelMenu, false);
            }
        }

        bool IsPanelVisible(OverlayPanel panel)
        {
            return panel switch
            {
                OverlayPanel.Speed => panelSpeed != null && panelSpeed.activeSelf,
                OverlayPanel.Menu => panelMenu != null && panelMenu.activeSelf,
                OverlayPanel.Pause => panelPause != null && panelPause.activeSelf,
                _ => false
            };
        }

        void ActivatePanelDeferred(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            UiPanelActivation.ShowDeferred(panel, this);
        }

        static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        void HidePanelsExcept(OverlayPanel keep)
        {
            if (panelSpeed != null)
            {
                panelSpeed.SetActive(keep == OverlayPanel.Speed);
            }

            if (panelMenu != null)
            {
                panelMenu.SetActive(keep == OverlayPanel.Menu);
            }

            if (panelPause != null)
            {
                panelPause.SetActive(keep == OverlayPanel.Pause);
            }

            _openPanel = keep;
        }

        void HideAllPanels()
        {
            _openPanel = OverlayPanel.None;
            settingsOpener?.Hide();

            if (panelSpeed != null)
            {
                panelSpeed.SetActive(false);
            }

            if (panelMenu != null)
            {
                panelMenu.SetActive(false);
            }

            if (panelPause != null)
            {
                panelPause.SetActive(false);
            }
        }

        /// <summary>
        /// Mục tiêu: Pause chia sẻ (pause/đầu hàng) hoặc pause local (settings) tùy chế độ.
        /// </summary>
        void SyncGameplayPause()
        {
            if (IsNetworkMatch)
            {
                PublishSharedOverlayState(_openPanel);
                SyncLocalSettingsPauseOnly();
                return;
            }

            if (HasSharedBlockingOverlay())
            {
                GamePauseService.Pause();
                return;
            }

            if (settingsOpener != null && settingsOpener.IsOpen)
            {
                GamePauseService.ApplyLocalSettingsPause(true);
                return;
            }

            GamePauseService.Resume();
        }

        void SyncLocalSettingsPauseOnly()
        {
            bool settingsOpen = settingsOpener != null && settingsOpener.IsOpen;
            GamePauseService.ApplyLocalSettingsPause(settingsOpen);
        }

        void PublishSharedOverlayState(OverlayPanel panel)
        {
            if (_applyingRemoteState)
            {
                return;
            }

            bool surrenderVisible = _surrenderDialogOpen
                || (surrenderConfirmDialog != null && surrenderConfirmDialog.IsVisible);
            bool sharedPause = panel == OverlayPanel.Pause || surrenderVisible;

            GameMatchOverlayStateSync.RequestSharedPanel(
                (byte)panel,
                sharedPause,
                surrenderVisible);
        }

        bool HasSharedBlockingOverlay()
        {
            if (_openPanel == OverlayPanel.Pause)
            {
                return true;
            }

            if (_surrenderDialogOpen || (surrenderConfirmDialog != null && surrenderConfirmDialog.IsVisible))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Client kia áp cùng panel/tốc độ/pause khi SyncVar đổi trên server.
        /// </summary>
        public static void ApplyRemoteSharedState(
            byte panelByte,
            bool sharedPaused,
            int speedPresetIndex,
            float speedScale,
            bool surrenderDialog)
        {
            InGameOverlayMenuController[] controllers = Object.FindObjectsByType<InGameOverlayMenuController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i]?.ApplyRemoteSharedStateInternal(
                    panelByte,
                    sharedPaused,
                    speedPresetIndex,
                    speedScale,
                    surrenderDialog);
            }
        }

        void ApplyRemoteSharedStateInternal(
            byte panelByte,
            bool sharedPaused,
            int speedPresetIndex,
            float speedScale,
            bool surrenderDialog)
        {
            _applyingRemoteState = true;
            try
            {
                _surrenderDialogOpen = surrenderDialog;
                _openPanel = (OverlayPanel)panelByte;

                settingsOpener?.Hide();

                ApplyPanelVisibilityFromRemote(_openPanel);

                if (speedSelectGroup != null && speedPresetIndex >= 0)
                {
                    speedSelectGroup.Select(speedPresetIndex, notify: false);
                }

                GamePauseService.ApplySharedSpeed(speedScale);
                GamePauseService.ApplySharedPause(sharedPaused);

                if (surrenderDialog && surrenderConfirmDialog != null && !surrenderConfirmDialog.IsVisible)
                {
                    surrenderConfirmDialog.ShowMatchEndConfirm(
                        SurrenderMessage,
                        OnSurrenderConfirmed,
                        OnSurrenderCancelled);
                }
                else if (!surrenderDialog && surrenderConfirmDialog != null && surrenderConfirmDialog.IsVisible)
                {
                    surrenderConfirmDialog.Hide();
                }
            }
            finally
            {
                _applyingRemoteState = false;
            }
        }

        void ApplyPanelVisibilityFromRemote(OverlayPanel panel)
        {
            SetPanelActive(panelSpeed, panel == OverlayPanel.Speed);
            SetPanelActive(panelMenu, panel == OverlayPanel.Menu);
            SetPanelActive(panelPause, panel == OverlayPanel.Pause);
        }

        public static void ApplyRemoteMatchEndFromSurrender()
        {
            MatchOutcomeDetector.ShowSurrenderDefeat(MatchOutcomeFlow.ActiveEndSceneName);
        }

        bool HasBlockingOverlay()
        {
            if (HasSharedBlockingOverlay())
            {
                return true;
            }

            if (settingsOpener != null && settingsOpener.IsOpen)
            {
                return true;
            }

            return false;
        }

        void TryResolveReferences(Transform root)
        {
            TryResolveButton(root, "Button Game Speed", ref buttonGameSpeed);
            TryResolveButton(root, "Button Game Menu", ref buttonGameMenu);
            TryResolveButton(root, "Button Speed", ref buttonGameSpeed);
            TryResolveButton(root, "Button Menu", ref buttonGameMenu);

            TryResolvePanel(root, "Panel Speed", ref panelSpeed);
            TryResolvePanel(root, "Panel Menu", ref panelMenu);
            TryResolvePanel(root, "Panel Pause", ref panelPause);

            TryResolveButton(root, "Button x0.5", ref buttonSpeedHalf);
            TryResolveButton(root, "Button x1", ref buttonSpeedNormal);
            TryResolveButton(root, "Button x1.5", ref buttonSpeedFast);
            TryResolveButton(root, "Button x2", ref buttonSpeedDouble);

            TryResolveButton(root, "Button Settings", ref buttonMenuSettings);
            TryResolveButton(root, "Button Pause", ref buttonMenuPause);
            TryResolveButton(root, "Button Surrender", ref buttonMenuSurrender);
            TryResolveButton(root, "Button Resume", ref buttonPauseResume);

            if (speedSelectGroup == null && panelSpeed != null)
            {
                speedSelectGroup = panelSpeed.GetComponent<UiExclusiveSelectGroup>();
                if (speedSelectGroup == null)
                {
                    speedSelectGroup = panelSpeed.AddComponent<UiExclusiveSelectGroup>();
                }
            }

            BootstrapSpeedOptions();

            if (settingsOpener == null)
            {
                settingsOpener = GetComponentInChildren<InGameSettingsPanelOpener>(true);
            }

            if (surrenderConfirmDialog == null)
            {
                surrenderConfirmDialog = GetComponentInChildren<ExitConfirmDialog>(true);
            }
        }

        void BootstrapSpeedOptions()
        {
            if (speedSelectGroup == null)
            {
                return;
            }

            EnsureSpeedOption(buttonSpeedHalf);
            EnsureSpeedOption(buttonSpeedNormal);
            EnsureSpeedOption(buttonSpeedFast);
            EnsureSpeedOption(buttonSpeedDouble);
        }

        static void EnsureSpeedOption(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (button.GetComponent<UiExclusiveSelectOption>() == null)
            {
                button.gameObject.AddComponent<UiExclusiveSelectOption>();
            }
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

        static void TryResolveButton(Transform root, string objectName, ref Button target)
        {
            if (target != null)
            {
                return;
            }

            Transform found = FindDeepChild(root, objectName);
            if (found != null)
            {
                target = found.GetComponent<Button>();
            }
        }

        static void TryResolvePanel(Transform root, string objectName, ref GameObject target)
        {
            if (target != null)
            {
                return;
            }

            Transform found = FindDeepChild(root, objectName);
            if (found != null)
            {
                target = found.gameObject;
            }
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
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
