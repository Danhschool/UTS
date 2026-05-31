using GameDevTV.RTS.Game.Pregame;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Điều khiển menu chính MainMenu — nút Play, dialog, thoát game.
    /// Gắn lên Canvas hoặc Panel; có thể auto-bind theo tên GameObject nếu để trống SerializeField.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuUIController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button buttonPvE;
        [SerializeField] Button buttonPvP;
        [SerializeField] Button buttonLearn;
        [SerializeField] Button buttonSetting;
        [SerializeField] Button buttonExit;

        [Header("Dialogs")]
        [SerializeField] GameObject dialogSetting;
        [SerializeField] GameObject dialogManual;
        [SerializeField] Button dialogSettingClose;
        [SerializeField] Button dialogManualClose;

        [Header("Exit confirm")]
        [SerializeField] ExitConfirmDialog exitConfirmDialog;

        [Header("Optional auto-bind root")]
        [SerializeField] Transform searchRoot;

        void Awake()
        {
            Transform root = searchRoot != null ? searchRoot : transform;

            if (exitConfirmDialog == null)
            {
                exitConfirmDialog = GetComponent<ExitConfirmDialog>();
            }

            TryResolveButton(root, "Button PvE", ref buttonPvE);
            TryResolveButton(root, "Button PvP", ref buttonPvP);
            TryResolveButton(root, "Button Learn", ref buttonLearn);
            TryResolveButton(root, "Button Setting", ref buttonSetting);
            TryResolveButton(root, "Button Exit", ref buttonExit);

            TryResolveDialog(root, "Dialog Setting", ref dialogSetting);
            TryResolveDialog(root, "Dialog Huong Dan", ref dialogManual);

            if (dialogSetting != null && dialogSettingClose == null)
            {
                dialogSettingClose = FindCloseButton(dialogSetting.transform);
            }

            if (dialogManual != null && dialogManualClose == null)
            {
                dialogManualClose = FindCloseButton(dialogManual.transform);
            }

            HideDialog(dialogSetting);
            HideDialog(dialogManual);
        }

        void OnEnable()
        {
            Bind(buttonPvE, OnSinglePlayerClicked);
            Bind(buttonPvP, OnMultiplayerClicked);
            Bind(buttonLearn, OnLearnClicked);
            Bind(buttonSetting, OnSettingClicked);
            Bind(buttonExit, OnExitClicked);
            Bind(dialogSettingClose, OnDialogSettingCloseClicked);
            Bind(dialogManualClose, OnDialogManualCloseClicked);
        }

        void OnDisable()
        {
            UnbindAll();
        }

        void OnDialogSettingCloseClicked()
        {
            HideDialog(dialogSetting);
        }

        void OnDialogManualCloseClicked()
        {
            HideDialog(dialogManual);
        }

        public void OnSinglePlayerClicked()
        {
            PregameMenuSceneNavigator.LoadSetup(PregamePlayMode.SinglePlayer);
        }

        public void OnMultiplayerClicked()
        {
            PregameMenuSceneNavigator.LoadSetup(PregamePlayMode.Multiplayer);
        }

        public void OnLearnClicked()
        {
            ShowDialog(dialogManual);
        }

        public void OnSettingClicked()
        {
            ShowDialog(dialogSetting);
        }

        public void OnExitClicked()
        {
            if (exitConfirmDialog == null)
            {
                Debug.LogWarning($"{nameof(MainMenuUIController)}: chưa gán {nameof(ExitConfirmDialog)}.", this);
                return;
            }

            exitConfirmDialog.Show();
        }

        public void ShowDialog(GameObject dialog)
        {
            if (dialog != null)
            {
                dialog.SetActive(true);
            }
        }

        public void HideDialog(GameObject dialog)
        {
            if (dialog != null)
            {
                dialog.SetActive(false);
            }
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        void UnbindAll()
        {
            Remove(buttonPvE, OnSinglePlayerClicked);
            Remove(buttonPvP, OnMultiplayerClicked);
            Remove(buttonLearn, OnLearnClicked);
            Remove(buttonSetting, OnSettingClicked);
            Remove(buttonExit, OnExitClicked);
            Remove(dialogSettingClose, OnDialogSettingCloseClicked);
            Remove(dialogManualClose, OnDialogManualCloseClicked);
        }

        static void Remove(Button button, UnityEngine.Events.UnityAction action)
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

        static void TryResolveDialog(Transform root, string objectName, ref GameObject target)
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

        static Button FindCloseButton(Transform dialogRoot)
        {
            Transform closeByName = FindDeepChild(dialogRoot, "Btn_Close");
            if (closeByName != null)
            {
                return closeByName.GetComponent<Button>();
            }

            Button[] buttons = dialogRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.Contains("Close") || buttons[i].name.Contains("Đóng"))
                {
                    return buttons[i];
                }
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                TMPro.TMP_Text label = buttons[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null && label.text.Contains("Đóng"))
                {
                    return buttons[i];
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
