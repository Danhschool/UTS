using System;
using GameDevTV.RTS.Game.Pregame;
using GameDevTV.RTS.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Dialog xác nhận 2 nút (OK / Hủy) — thoát app hoặc hành động tùy chỉnh (đầu hàng…).
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

        Action _customConfirmAction;
        Action _customCancelAction;

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
        /// Mục tiêu: Hiện dialog thoát game mặc định (quit application).
        /// Cách hoạt động: Xóa custom action, dùng message mặc định hoặc đã gán trong Inspector.
        /// </summary>
        public void Show()
        {
            Show(message, null);
        }

        /// <summary>
        /// Mục tiêu: Hiện dialog với message và hành động xác nhận tùy chỉnh (ví dụ đầu hàng).
        /// Cách hoạt động: Lưu delegate; OK gọi delegate, Hủy chỉ đóng dialog.
        /// </summary>
        public void Show(string messageOverride, Action onConfirm)
        {
            Show(messageOverride, onConfirm, null);
        }

        /// <summary>
        /// Mục tiêu: Hiện dialog với message, OK và Hủy tùy chỉnh (ví dụ đầu hàng / tiếp tục chơi).
        /// Cách hoạt động: Lưu delegate confirm/cancel; Hủy gọi onCancel sau khi ẩn dialog.
        /// </summary>
        public void Show(string messageOverride, Action onConfirm, Action onCancel)
        {
            if (dialogRoot == null)
            {
                Debug.LogWarning($"{nameof(ExitConfirmDialog)}: chưa gán Dialog Root.", this);
                return;
            }

            _customConfirmAction = onConfirm;
            _customCancelAction = onCancel;
            message = string.IsNullOrWhiteSpace(messageOverride) ? DefaultMessage : messageOverride;
            ApplyMessage();
            UiPanelActivation.ShowDeferred(dialogRoot, this);
        }

        public bool IsVisible => dialogRoot != null && dialogRoot.activeSelf;

        public void Hide()
        {
            if (dialogRoot != null)
            {
                dialogRoot.SetActive(false);
            }

            _customConfirmAction = null;
            _customCancelAction = null;
        }

        void OnConfirmClicked()
        {
            Action confirm = _customConfirmAction;
            Hide();

            if (confirm != null)
            {
                confirm.Invoke();
                return;
            }

            PregameApplicationQuit.RequestQuit();
        }

        void OnCancelClicked()
        {
            Action cancel = _customCancelAction;
            Hide();
            cancel?.Invoke();
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
