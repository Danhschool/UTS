using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Chặn hotkey khi người chơi đang focus ô nhập liệu (chat, search…).
    /// </summary>
    public sealed class UiFocusHotkeyGate : IHotkeyGate
    {
        public bool IsBlocked(in HotkeyContext context)
        {
            if (!IsTextInputFocused())
            {
                return false;
            }

            // Esc luôn được phép (đóng chat / hủy placement).
            return context.Id != HotkeyId.CancelSelection;
        }

        static bool IsTextInputFocused()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (selected == null)
            {
                return false;
            }

            return selected.GetComponent<TMP_InputField>() != null
                || selected.GetComponent<UnityEngine.UI.InputField>() != null;
        }
    }
}
