using GameDevTV.RTS.Game;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Panel kết thúc trận — tiêu đề thắng/thua, thống kê 2 phe, nút Close về MainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchOutcomeSummaryPanelController : MonoBehaviour
    {
        static MatchOutcomeSummaryPanelController instance;

        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_Text outcomeTitleText;
        [SerializeField] FactionSummaryScrollViewBinder scrollBinder;
        [SerializeField] Button closeButton;
        [SerializeField] string mainMenuSceneName = MatchOutcomeFlow.DefaultMenuSceneName;
        [SerializeField] Color victoryTitleColor = new(1f, 0.84f, 0.2f, 1f);
        [SerializeField] Color defeatTitleColor = new(1f, 0.35f, 0.35f, 1f);

        public static MatchOutcomeSummaryPanelController Instance => instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (scrollBinder == null)
            {
                scrollBinder = GetComponentInChildren<FactionSummaryScrollViewBinder>(true);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            SetPanelVisible(false);
        }

        void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Mục tiêu: Tìm panel trên HUD gameplay.
        /// </summary>
        public static MatchOutcomeSummaryPanelController EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            MatchOutcomeSummaryPanelController existing = FindFirstObjectByType<MatchOutcomeSummaryPanelController>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return null;
        }

        /// <summary>
        /// Mục tiêu: Hiện màn hình kết thúc với thống kê 2 phe.
        /// Cách hoạt động: Gán tiêu đề, refresh scroll binder dual-owner, pause game.
        /// </summary>
        public void Show(MatchOutcomeResult result, Owner localOwner, Owner opponentOwner)
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            ResolveReferences();
            SetPanelVisible(true);

            if (outcomeTitleText != null)
            {
                outcomeTitleText.text = MatchOutcomeResolver.GetTitle(result);
                outcomeTitleText.color = result == MatchOutcomeResult.Victory
                    ? victoryTitleColor
                    : defeatTitleColor;
            }

            scrollBinder?.RefreshDual(localOwner, opponentOwner);
            GamePauseService.Pause();
        }

        void OnCloseClicked()
        {
            SetPanelVisible(false);
            MatchOutcomeFlow.LoadMenuScene(mainMenuSceneName);
        }

        void SetPanelVisible(bool active)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(active);
            }
        }

        void ResolveReferences()
        {
            outcomeTitleText ??= GetComponentInChildren<TMP_Text>(true);
            scrollBinder ??= GetComponentInChildren<FactionSummaryScrollViewBinder>(true);

            if (closeButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].gameObject.name.Contains("Close"))
                    {
                        closeButton = buttons[i];
                        break;
                    }
                }
            }
        }
    }
}
