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
        bool _requireCustomConfirm;

        void Awake()
        {
            // Không gọi Hide() ở đây: Awake chạy lần đầu khi dialogRoot được SetActive(true),
            // Hide() sẽ xóa _customConfirmAction ngay sau Show() (đầu hàng lần 1 fail).
            ApplyMessage();
            WireButtonsOnce();
        }

        /// <summary>
        /// Mục tiêu: Tránh listener trùng trên OK/Hủy (OnEnable nhiều lần → OK gọi 2 lần, lần 2 thoát game).
        /// Cách hoạt động: Gắn listener một lần trong Awake; Remove trước Add để dedupe.
        /// </summary>
        void WireButtonsOnce()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
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
            _requireCustomConfirm = onConfirm != null;
            message = string.IsNullOrWhiteSpace(messageOverride) ? DefaultMessage : messageOverride;
            ApplyMessage();
            WireButtonsOnce();
            UiPanelActivation.ShowDeferred(dialogRoot, this);
        }

        /// <summary>
        /// Mục tiêu: Xác nhận đầu hàng / kết thúc trận — không bao giờ fallback thoát game.
        /// </summary>
        public void ShowMatchEndConfirm(string messageOverride, Action onConfirm, Action onCancel)
        {
            if (onConfirm == null)
            {
                Debug.LogError(
                    $"{nameof(ExitConfirmDialog)}.{nameof(ShowMatchEndConfirm)}: thiếu onConfirm.",
                    this);
                return;
            }

            _requireCustomConfirm = true;
            Show(messageOverride, onConfirm, onCancel);
            // Awake có thể vừa chạy sau SetActive(true) — gán lại delegate sau khi dialog sẵn sàng.
            _customConfirmAction = onConfirm;
            _customCancelAction = onCancel;
            _requireCustomConfirm = true;
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
            _requireCustomConfirm = false;
        }

        void OnConfirmClicked()
        {
            Action confirm = _customConfirmAction;
            bool requireCustom = _requireCustomConfirm;

            Hide();

            if (confirm != null)
            {
                confirm.Invoke();
                return;
            }

            if (requireCustom)
            {
                Debug.LogWarning(
                    $"{nameof(ExitConfirmDialog)}: OK thiếu custom action trong chế độ xác nhận tùy chỉnh — không thoát game.",
                    this);
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
