using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Phím V (cấu hình được) → bắt đầu thu một câu thoại (SRP: chỉ input).
    /// Dùng Input System giống <see cref="GameDevTV.RTS.Player.PlayerInput"/> trong scene chính.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandPushToTalkInput : MonoBehaviour
    {
        [SerializeField] private VoiceCommandOneShotCapture _capture;
        [SerializeField] private KeyCode _captureKey = KeyCode.V;

        private void Awake()
        {
            if (_capture == null)
            {
                _capture = GetComponent<VoiceCommandOneShotCapture>();
            }
        }

        private void Update()
        {
            if (_capture == null || !WasCaptureKeyPressedThisFrame())
            {
                return;
            }

            if (!_capture.IsCapturing)
            {
                _capture.BeginCapture();
            }
        }

        /// <summary>
        /// Mục tiêu: Nhận phím V trong scene dùng New Input System (Game 1).
        /// Cách hoạt động: Ưu tiên Keyboard.current; fallback Input.GetKeyDown khi Both/Legacy.
        /// </summary>
        private bool WasCaptureKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return _captureKey switch
                {
                    KeyCode.V => keyboard.vKey.wasPressedThisFrame,
                    KeyCode.B => keyboard.bKey.wasPressedThisFrame,
                    KeyCode.Space => keyboard.spaceKey.wasPressedThisFrame,
                    _ => keyboard.vKey.wasPressedThisFrame
                };
            }
#endif
            return Input.GetKeyDown(_captureKey);
        }
    }
}
