using System;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Game.Pregame;
using GameDevTV.RTS.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Màn setup SSScene — chọn map, độ khó AI (SP), bắt đầu trận hoặc quay menu.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PregameSetupUIController : MonoBehaviour
    {
        [Serializable]
        public struct MapEntry
        {
            public string displayName;
            public Sprite previewSprite;
            public string gameplaySceneName;
        }

        [Header("Mode panels")]
        [SerializeField] GameObject singlePlayerAiPanel;
        [SerializeField] GameObject multiplayerPanel;

        [Header("Map preview")]
        [SerializeField] Image mapPreviewImage;
        [SerializeField] TMP_Text mapNameLabel;
        [SerializeField] UiExclusiveSelectGroup mapSelectGroup;
        [SerializeField] MapEntry[] maps =
        {
            new() { displayName = "Map 1", gameplaySceneName = PregameSessionState.DefaultGameplayScene },
            new() { displayName = "Map 2", gameplaySceneName = PregameSessionState.DefaultGameplayScene },
            new() { displayName = "Map 3", gameplaySceneName = PregameSessionState.DefaultGameplayScene },
            new() { displayName = "Map 4", gameplaySceneName = PregameSessionState.DefaultGameplayScene }
        };

        [Header("AI difficulty (single player)")]
        [SerializeField] Transform difficultyOptionsRoot;
        [SerializeField] UiExclusiveSelectGroup difficultySelectGroup;
        [SerializeField] string[] difficultyRowNames = { "Easy", "Medium", "Hard" };
        [SerializeField] AIDifficultyLevel[] difficultyLevels =
        {
            AIDifficultyLevel.Easy,
            AIDifficultyLevel.Medium,
            AIDifficultyLevel.Hard
        };

        [Header("Actions")]
        [SerializeField] Button buttonStart;
        [SerializeField] Button buttonBack;
        [SerializeField] Button buttonExit;
        [SerializeField] Button buttonCreateRoom;
        [SerializeField] bool createExitButtonIfMissing = true;

        [Header("Exit confirm")]
        [SerializeField] ExitConfirmDialog exitConfirmDialog;

        [Header("Optional auto-bind")]
        [SerializeField] Transform searchRoot;

        void Awake()
        {
            Transform root = searchRoot != null ? searchRoot : transform;

            if (exitConfirmDialog == null)
            {
                exitConfirmDialog = GetComponent<ExitConfirmDialog>();
            }

            TryResolvePanel(root, "AI Panel", ref singlePlayerAiPanel);
            TryResolvePanel(root, "MP Panel", ref multiplayerPanel);
            TryResolveButton(root, "Button Start", ref buttonStart);
            TryResolveButton(root, "Button Back", ref buttonBack);
            TryResolveButton(root, "Button Exit", ref buttonExit);
            TryResolveButton(root, "Button Start (1)", ref buttonCreateRoom);

            if (buttonExit == null && createExitButtonIfMissing)
            {
                TryCreateExitButtonFromBack();
            }

            if (mapPreviewImage == null)
            {
                Transform preview = FindDeepChild(root, "Image_Map");
                if (preview != null)
                {
                    mapPreviewImage = preview.GetComponent<Image>();
                }
            }

            if (mapNameLabel == null)
            {
                Transform label = FindDeepChild(root, "Map name");
                if (label != null)
                {
                    mapNameLabel = label.GetComponent<TMP_Text>();
                }
            }

            if (mapSelectGroup == null)
            {
                Transform mapScrollContent = FindMapListContent(root);
                if (mapScrollContent != null)
                {
                    mapSelectGroup = mapScrollContent.GetComponent<UiExclusiveSelectGroup>();
                    if (mapSelectGroup == null)
                    {
                        mapSelectGroup = mapScrollContent.gameObject.AddComponent<UiExclusiveSelectGroup>();
                    }
                }
            }

            BootstrapMapSelectOptions();
            BootstrapDifficultySelectOptions();

            mapSelectGroup?.RefreshOptions();
            difficultySelectGroup?.RefreshOptions();

            ApplyPlayMode(PregameSessionState.PlayMode);
        }

        void OnEnable()
        {
            ApplyPlayMode(PregameSessionState.PlayMode);

            if (mapSelectGroup != null)
            {
                mapSelectGroup.SelectionChanged += OnMapSelectionChanged;
                mapSelectGroup.Select(PregameSessionState.SelectedMapIndex, notify: true);
            }

            if (difficultySelectGroup != null && PregameSessionState.PlayMode == PregamePlayMode.SinglePlayer)
            {
                difficultySelectGroup.SelectionChanged += OnDifficultySelectionChanged;
                int difficultyIndex = DifficultyLevelToIndex(PregameSessionState.SelectedDifficulty);
                difficultySelectGroup.Select(difficultyIndex, notify: true);
            }

            Bind(buttonStart, OnStartClicked);
            Bind(buttonBack, OnBackClicked);
            Bind(buttonExit, OnExitClicked);
            Bind(buttonCreateRoom, OnCreateRoomClicked);
        }

        void OnDisable()
        {
            if (mapSelectGroup != null)
            {
                mapSelectGroup.SelectionChanged -= OnMapSelectionChanged;
            }

            if (difficultySelectGroup != null)
            {
                difficultySelectGroup.SelectionChanged -= OnDifficultySelectionChanged;
            }

            Unbind(buttonStart, OnStartClicked);
            Unbind(buttonBack, OnBackClicked);
            Unbind(buttonExit, OnExitClicked);
            Unbind(buttonCreateRoom, OnCreateRoomClicked);
        }

        void ApplyPlayMode(PregamePlayMode mode)
        {
            bool singlePlayer = mode == PregamePlayMode.SinglePlayer;

            if (singlePlayerAiPanel != null)
            {
                singlePlayerAiPanel.SetActive(singlePlayer);
            }

            if (multiplayerPanel != null)
            {
                multiplayerPanel.SetActive(!singlePlayer);
            }

            if (buttonStart != null)
            {
                buttonStart.gameObject.SetActive(singlePlayer);
            }

            if (buttonCreateRoom != null)
            {
                buttonCreateRoom.gameObject.SetActive(!singlePlayer);
            }
        }

        void OnMapSelectionChanged(int index)
        {
            if (maps == null || maps.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, maps.Length - 1);
            MapEntry entry = maps[index];

            if (mapNameLabel != null)
            {
                mapNameLabel.text = entry.displayName;
            }

            if (mapPreviewImage != null && entry.previewSprite != null)
            {
                mapPreviewImage.sprite = entry.previewSprite;
                mapPreviewImage.enabled = true;
            }
        }

        void OnDifficultySelectionChanged(int index)
        {
            PregameSessionState.SetSelectedDifficulty(ResolveDifficultyAt(index));
        }

        public void OnStartClicked()
        {
            int mapIndex = mapSelectGroup != null ? mapSelectGroup.SelectedIndex : 0;
            string sceneName = ResolveGameplayScene(mapIndex);

            if (PregameSessionState.PlayMode == PregamePlayMode.SinglePlayer)
            {
                AIDifficultyLevel difficulty = ResolveDifficulty();
                PregameSessionState.ConfigureSinglePlayer(mapIndex, difficulty, sceneName);
                PregameMenuSceneNavigator.StartGameplay(sceneName);
                return;
            }

            PregameSessionState.ConfigureMultiplayer(mapIndex, sceneName);
            PregameMenuSceneNavigator.LoadLobby();
        }

        public void OnBackClicked()
        {
            PregameMenuSceneNavigator.LoadMainMenu();
        }

        public void OnExitClicked()
        {
            if (exitConfirmDialog == null)
            {
                Debug.LogWarning($"{nameof(PregameSetupUIController)}: chưa gán {nameof(ExitConfirmDialog)}.", this);
                return;
            }

            exitConfirmDialog.Show();
        }

        public void OnCreateRoomClicked()
        {
            int mapIndex = mapSelectGroup != null ? mapSelectGroup.SelectedIndex : 0;
            PregameSessionState.ConfigureMultiplayer(mapIndex, ResolveGameplayScene(mapIndex));
            PregameMenuSceneNavigator.LoadLobby();
        }

        AIDifficultyLevel ResolveDifficulty()
        {
            if (difficultySelectGroup == null || difficultySelectGroup.SelectedIndex < 0)
            {
                return PregameSessionState.SelectedDifficulty;
            }

            return ResolveDifficultyAt(difficultySelectGroup.SelectedIndex);
        }

        string ResolveGameplayScene(int mapIndex)
        {
            if (maps == null || maps.Length == 0)
            {
                return PregameSessionState.DefaultGameplayScene;
            }

            mapIndex = Mathf.Clamp(mapIndex, 0, maps.Length - 1);
            return string.IsNullOrWhiteSpace(maps[mapIndex].gameplaySceneName)
                ? PregameSessionState.DefaultGameplayScene
                : maps[mapIndex].gameplaySceneName;
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

        /// <summary>
        /// Mục tiêu: Có nút Thoát trên SSScene khi scene chưa có Button Exit.
        /// Cách hoạt động: Nhân bản Button Back, đặt bên trái, đổi nhãn "Thoát".
        /// </summary>
        void TryCreateExitButtonFromBack()
        {
            if (buttonBack == null)
            {
                return;
            }

            buttonExit = Instantiate(buttonBack, buttonBack.transform.parent);
            buttonExit.name = "Button Exit";

            TMP_Text label = buttonExit.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "Thoát\n";
            }

            RectTransform backRect = buttonBack.GetComponent<RectTransform>();
            RectTransform exitRect = buttonExit.GetComponent<RectTransform>();
            if (backRect != null && exitRect != null)
            {
                exitRect.anchorMin = backRect.anchorMin;
                exitRect.anchorMax = backRect.anchorMax;
                exitRect.pivot = backRect.pivot;
                exitRect.sizeDelta = backRect.sizeDelta;
                exitRect.anchoredPosition = backRect.anchoredPosition + new Vector2(-(backRect.sizeDelta.x + 12f), 0f);
            }
        }

        void BootstrapMapSelectOptions()
        {
            if (mapSelectGroup == null)
            {
                return;
            }

            Transform content = FindMapListContent(searchRoot != null ? searchRoot : transform);
            if (content == null)
            {
                return;
            }

            Button[] buttons = content.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!IsMapListButton(buttons[i]))
                {
                    continue;
                }

                if (buttons[i].GetComponent<UiExclusiveSelectOption>() == null)
                {
                    buttons[i].gameObject.AddComponent<UiExclusiveSelectOption>();
                }
            }
        }

        static bool IsMapListButton(Button button)
        {
            if (button == null)
            {
                return false;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null || string.IsNullOrWhiteSpace(label.text))
            {
                return false;
            }

            string trimmed = label.text.Trim();
            return trimmed.StartsWith("Map", System.StringComparison.OrdinalIgnoreCase);
        }

        void BootstrapDifficultySelectOptions()
        {
            if (singlePlayerAiPanel == null)
            {
                return;
            }

            Transform optionsRoot = ResolveDifficultyOptionsRoot();
            if (optionsRoot == null)
            {
                return;
            }

            if (difficultySelectGroup == null)
            {
                difficultySelectGroup = optionsRoot.GetComponent<UiExclusiveSelectGroup>();
                if (difficultySelectGroup == null)
                {
                    difficultySelectGroup = optionsRoot.gameObject.AddComponent<UiExclusiveSelectGroup>();
                }
            }

            string[] rows = difficultyRowNames is { Length: > 0 }
                ? difficultyRowNames
                : new[] { "Easy", "Medium", "Hard" };

            for (int i = 0; i < rows.Length; i++)
            {
                Transform row = FindDeepChild(singlePlayerAiPanel.transform, rows[i]);
                if (row == null)
                {
                    Debug.LogWarning($"{nameof(PregameSetupUIController)}: Không tìm thấy hàng độ khó '{rows[i]}'.", this);
                    continue;
                }

                ConfigureDifficultyRow(row);
            }

            difficultySelectGroup.RefreshOptions();
        }

        Transform ResolveDifficultyOptionsRoot()
        {
            if (difficultyOptionsRoot != null)
            {
                return difficultyOptionsRoot;
            }

            difficultyOptionsRoot = singlePlayerAiPanel.transform;
            return difficultyOptionsRoot;
        }

        /// <summary>
        /// Mục tiêu: Mỗi hàng độ khó = Button (Img Select + Img Unselect) + Text bên cạnh.
        /// Cách hoạt động: Gắn UiExclusiveSelectOption lên Button con; text không chặn raycast.
        /// </summary>
        static void ConfigureDifficultyRow(Transform row)
        {
            RemoveRedundantRowButton(row);

            Transform buttonRoot = FindDeepChild(row, "Button");
            Button difficultyButton = buttonRoot != null
                ? buttonRoot.GetComponent<Button>()
                : row.GetComponent<Button>();

            if (difficultyButton == null)
            {
                Debug.LogWarning($"[PregameSetup] Hàng '{row.name}' cần Button trên hàng hoặc child 'Button'.", row);
                return;
            }

            if (buttonRoot == null)
            {
                buttonRoot = difficultyButton.transform;
            }

            difficultyButton.interactable = true;

            TMP_Text[] labels = row.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].raycastTarget = false;
            }

            Image rowBackground = row.GetComponent<Image>();
            if (rowBackground != null && rowBackground.gameObject != buttonRoot.gameObject)
            {
                rowBackground.raycastTarget = false;
            }

            GameObject selectVisual = FindChildGameObject(row, "Img Select");
            GameObject unselectVisual = FindChildGameObject(row, "Img Unselect");

            UiExclusiveSelectOption option = row.GetComponent<UiExclusiveSelectOption>();
            if (option == null)
            {
                option = buttonRoot.GetComponent<UiExclusiveSelectOption>();
            }

            if (option == null)
            {
                option = buttonRoot.gameObject.AddComponent<UiExclusiveSelectOption>();
            }

            option.Configure(difficultyButton, selectVisual, unselectVisual);
        }

        static void RemoveRedundantRowButton(Transform row)
        {
            Button rowButton = row.GetComponent<Button>();
            if (rowButton == null)
            {
                return;
            }

            Transform nestedButton = FindDeepChild(row, "Button");
            if (nestedButton != null)
            {
                UnityEngine.Object.Destroy(rowButton);
            }
        }

        static GameObject FindChildGameObject(Transform parent, string childName)
        {
            Transform found = FindDeepChild(parent, childName);
            return found != null ? found.gameObject : null;
        }

        static int DifficultyLevelToIndex(AIDifficultyLevel level)
        {
            return level switch
            {
                AIDifficultyLevel.Easy => 0,
                AIDifficultyLevel.Hard => 2,
                _ => 1
            };
        }

        AIDifficultyLevel ResolveDifficultyAt(int index)
        {
            if (difficultyLevels == null || difficultyLevels.Length == 0)
            {
                return AIDifficultyLevel.Medium;
            }

            index = Mathf.Clamp(index, 0, difficultyLevels.Length - 1);
            return difficultyLevels[index];
        }

        static Transform FindMapListContent(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != "Map Panel")
                {
                    continue;
                }

                Transform content = FindDeepChild(all[i], "Content");
                if (content != null && content.GetComponentInChildren<Button>(true) != null)
                {
                    return content;
                }
            }

            return null;
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
