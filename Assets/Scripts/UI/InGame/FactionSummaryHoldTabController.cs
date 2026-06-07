using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Game.Startup;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Giữ Tab để mở panel tóm tắt; thả Tab để đóng — không pause game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryHoldTabController : MonoBehaviour
    {
        [SerializeField] GameObject summaryPanelRoot;
        [SerializeField] FactionSummaryScrollViewBinder scrollBinder;
        [SerializeField, Min(0.05f)] float refreshIntervalSeconds = 0.25f;

        bool panelVisible;
        float nextRefreshTime;

        void Awake()
        {
            if (summaryPanelRoot == null)
            {
                summaryPanelRoot = gameObject;
            }

            if (scrollBinder == null)
            {
                scrollBinder = GetComponentInChildren<FactionSummaryScrollViewBinder>(true);
            }

            MatchOutcomeDetector.EnsureExists();
            SetPanelVisible(false);
        }

        void Update()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                if (panelVisible)
                {
                    SetPanelVisible(false);
                }

                return;
            }

            FactionSummaryTracker.EnsureExists();

            bool tabHeld = IsTabHeld();
            if (tabHeld)
            {
                if (!panelVisible)
                {
                    SetPanelVisible(true);
                    RefreshNow();
                }
                else if (Time.unscaledTime >= nextRefreshTime)
                {
                    RefreshNow();
                }
            }
            else if (panelVisible)
            {
                SetPanelVisible(false);
            }
        }

        static bool IsTabHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tabKey.isPressed;
        }

        void RefreshNow()
        {
            nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            scrollBinder?.Refresh();
        }

        void SetPanelVisible(bool visible)
        {
            panelVisible = visible;
            if (summaryPanelRoot != null)
            {
                summaryPanelRoot.SetActive(visible);
            }
        }
    }
}
