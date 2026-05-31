using GameDevTV.RTS.Game.Pregame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Hiện dialog xác nhận thoát đã làm sẵn trong scene/prefab (OK → quit, Hủy → đóng).
    /// Gán Dialog Root, Txt_Message, Btn_Confirm, Btn_Cancel trong Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExitConfirmDialog : MonoBehaviour
    {
        const string DefaultMessage = "Bạn có chắc muốn thoát game không?";

        [SerializeField] GameObject dialogRoot;
        [SerializeField] TMP_Text messageLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] string message = DefaultMessage;

        void Awake()
        {
            Hide();
            ApplyMessage();
        }

        void OnEnable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }
        }

        void OnDisable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
            }
        }

        /// <summary>
        /// Mục tiêu: Hiện dialog hỏi xác nhận khi bấm Thoát.
        /// Cách hoạt động: Bật dialogRoot đã gán sẵn; chờ OK/Hủy.
        /// </summary>
        public void Show()
        {
            if (dialogRoot == null)
            {
                Debug.LogWarning($"{nameof(ExitConfirmDialog)}: chưa gán Dialog Root.", this);
                return;
            }

            ApplyMessage();
            dialogRoot.SetActive(true);
        }

        public void Hide()
        {
            if (dialogRoot != null)
            {
                dialogRoot.SetActive(false);
            }
        }

        void OnConfirmClicked()
        {
            Hide();
            PregameApplicationQuit.RequestQuit();
        }

        void OnCancelClicked()
        {
            Hide();
        }

        void ApplyMessage()
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }
        }
    }
}
