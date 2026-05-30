using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Phím giữ để thu thoại (SRP: chỉ input qua Input System).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandPushToTalkInput : MonoBehaviour
    {
        [SerializeField] VoiceCommandOneShotCapture _capture;
        [FormerlySerializedAs("_captureKey")]
        [SerializeField] Key captureKey = Key.V;

        void Awake()
        {
            if (_capture == null)
            {
                _capture = GetComponent<VoiceCommandOneShotCapture>();
            }

            captureKey = InputSystemKeyboardUtility.CoerceKeyboardKey(captureKey, Key.V);
        }

        void Reset()
        {
            captureKey = Key.V;
        }

        void Update()
        {
            if (_capture == null || !InputSystemKeyboardUtility.WasPressedThisFrame(captureKey))
            {
                return;
            }

            if (!_capture.IsCapturing)
            {
                _capture.BeginCapture();
            }
        }
    }
}
