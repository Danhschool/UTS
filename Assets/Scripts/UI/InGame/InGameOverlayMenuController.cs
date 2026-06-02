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

        [Header("Defeat")]
        [SerializeField] string defeatSceneName = MatchOutcomeFlow.DefaultDefeatScene;

        [Header("Optional auto-bind")]
        [SerializeField] Transform searchRoot;

        OverlayPanel _openPanel = OverlayPanel.None;
        bool _surrenderDialogOpen;

        enum OverlayPanel
        {
            None = 0,
            Speed = 1,
            Menu = 2,
            Pause = 3
        }

        void Awake()
        {
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
            SyncGameplayPause();
        }

        void OnSettingsPanelClosed()
        {
            SyncGameplayPause();
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
            SyncGameplayPause();
            surrenderConfirmDialog.Show(SurrenderMessage, OnSurrenderConfirmed, OnSurrenderCancelled);
        }

        void OnSurrenderCancelled()
        {
            _surrenderDialogOpen = false;
            SyncGameplayPause();
        }

        void OnPauseResumeClicked()
        {
            settingsOpener?.Hide();
            HideAllPanels();
            SyncGameplayPause();
        }

        void OnSurrenderConfirmed()
        {
            _surrenderDialogOpen = false;
            HideAllPanels();
            settingsOpener?.Hide();
            MatchOutcomeFlow.LoadDefeatScene(defeatSceneName);
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
        /// Mục tiêu: Chỉ pause khi mở pause/settings/dialog đầu hàng — không pause khi mở panel speed/menu.
        /// Cách hoạt động: HasBlockingOverlay true → Pause; false → Resume.
        /// </summary>
        void SyncGameplayPause()
        {
            if (HasBlockingOverlay())
            {
                GamePauseService.Pause();
                return;
            }

            GamePauseService.Resume();
        }

        bool HasBlockingOverlay()
        {
            if (_openPanel == OverlayPanel.Pause)
            {
                return true;
            }

            if (settingsOpener != null && settingsOpener.IsOpen)
            {
                return true;
            }

            if (_surrenderDialogOpen || (surrenderConfirmDialog != null && surrenderConfirmDialog.IsVisible))
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
