using System;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI.Pregame;
using UnityEngine;

using UnityEngine.Events;

using UnityEngine.UI;



namespace GameDevTV.RTS.UI.InGame

{

    /// <summary>

    /// SRP: Bật/tắt panel cài đặt in-game (dùng chung MainMenuSettingsDialogController).

    /// </summary>

    [DisallowMultipleComponent]

    public sealed class InGameSettingsPanelOpener : MonoBehaviour

    {

        [SerializeField] GameObject settingsPanelRoot;

        [SerializeField] MainMenuSettingsDialogController settingsDialog;

        [SerializeField] Button buttonClose;



        public bool IsOpen => settingsPanelRoot != null && settingsPanelRoot.activeSelf;



        /// <summary>Panel đã đóng (sau nút Close hoặc Hide).</summary>

        public event Action Closed;



        void Awake()

        {

            if (settingsDialog == null && settingsPanelRoot != null)

            {

                settingsDialog = settingsPanelRoot.GetComponentInChildren<MainMenuSettingsDialogController>(true);

            }



            if (buttonClose == null && settingsPanelRoot != null)

            {

                buttonClose = FindCloseButton(settingsPanelRoot.transform);

            }



            Hide();

        }



        void OnEnable()

        {

            Bind(buttonClose, OnCloseClicked);

        }



        void OnDisable()

        {

            Unbind(buttonClose, OnCloseClicked);

        }



        /// <summary>

        /// Mục tiêu: Mở dialog cài đặt giống MainMenu (audio, hotkey, tên).

        /// Cách hoạt động: Bật panel root; MainMenuSettingsDialogController tự load staging trong OnEnable.

        /// </summary>

        public void Show()

        {

            if (settingsPanelRoot == null)

            {

                Debug.LogWarning($"{nameof(InGameSettingsPanelOpener)}: chưa gán settings panel.", this);

                return;

            }



            UiPanelActivation.ShowDeferred(settingsPanelRoot, this);
        }



        /// <summary>

        /// Mục tiêu: Ẩn panel cài đặt và báo cho HUD overlay (resume gameplay).

        /// Cách hoạt động: SetActive(false) trên root; phát Closed nếu panel đang mở.

        /// </summary>

        public void Hide()

        {

            if (settingsPanelRoot == null)

            {

                return;

            }



            bool wasOpen = settingsPanelRoot.activeSelf;

            settingsPanelRoot.SetActive(false);



            if (wasOpen)

            {

                Closed?.Invoke();

            }

        }



        void OnCloseClicked()

        {

            Hide();

        }



        static void Bind(Button button, UnityAction action)

        {

            if (button != null)

            {

                button.onClick.AddListener(action);

            }

        }



        static void Unbind(Button button, UnityAction action)

        {

            if (button != null)

            {

                button.onClick.RemoveListener(action);

            }

        }



        static Button FindCloseButton(Transform dialogRoot)

        {

            if (dialogRoot == null)

            {

                return null;

            }



            Transform closeByName = FindDeepChild(dialogRoot, "Btn_Close");

            if (closeByName != null)

            {

                Button byName = closeByName.GetComponent<Button>();

                if (byName != null)

                {

                    return byName;

                }

            }



            Button[] buttons = dialogRoot.GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)

            {

                string n = buttons[i].name;

                if (n.Contains("Close", StringComparison.OrdinalIgnoreCase) ||

                    n.Contains("Đóng", StringComparison.OrdinalIgnoreCase))

                {

                    return buttons[i];

                }

            }



            return null;

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


