using GameDevTV.RTS.Game;
using GameDevTV.RTS.UI.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.End
{
    /// <summary>
    /// SRP: Scene End — hiển thị thắng/thua, thống kê 2 phe, nút Close về MainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndSceneOutcomeController : MonoBehaviour
    {
        [SerializeField] TMP_Text outcomeTitleText;
        [SerializeField] FactionSummaryScrollViewBinder scrollBinder;
        [SerializeField] Button closeButton;
        [SerializeField] string mainMenuSceneName = MatchOutcomeFlow.DefaultMenuSceneName;
        [SerializeField] Color victoryTitleColor = new(1f, 0.84f, 0.2f, 1f);
        [SerializeField] Color defeatTitleColor = new(1f, 0.35f, 0.35f, 1f);

        void Awake()
        {
            ResolveReferences();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
            }
        }

        void Start()
        {
            GamePauseService.ForceResumeForSceneChange();

            if (!MatchOutcomeSessionState.TryConsume(out MatchOutcomeSessionState.Payload payload))
            {
                Debug.LogWarning("[EndSceneOutcomeController] Không có dữ liệu kết quả trận — quay về MainMenu.");
                MatchOutcomeFlow.LoadMenuScene(mainMenuSceneName);
                return;
            }

            ApplyOutcome(payload);
        }

        void ApplyOutcome(MatchOutcomeSessionState.Payload payload)
        {
            if (outcomeTitleText != null)
            {
                outcomeTitleText.gameObject.SetActive(true);
                outcomeTitleText.text = MatchOutcomeResolver.GetTitle(payload.Result);
                outcomeTitleText.color = payload.Result == MatchOutcomeResult.Victory
                    ? victoryTitleColor
                    : defeatTitleColor;
            }

            scrollBinder?.RefreshDualFromSession(
                payload.LocalFactionLabel,
                payload.LocalSummary,
                payload.OpponentFactionLabel,
                payload.OpponentSummary);
        }

        void OnCloseClicked()
        {
            MatchOutcomeFlow.LoadMenuScene(mainMenuSceneName);
        }

        void ResolveReferences()
        {
            outcomeTitleText ??= FindOutcomeTitleText();
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

        TMP_Text FindOutcomeTitleText()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == "Title")
                {
                    return texts[i];
                }
            }

            return GetComponentInChildren<TMP_Text>(true);
        }
    }
}
