using GameDevTV.RTS.UI.GameEventLog;
using ProjectRTS.SpeechRecognition.Whisper;
using UnityEngine;
using Whisper;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Log một lần khi Play để kiểm tra voice đã gắn trong scene chính (SRP: chẩn đoán).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandRuntimeDiagnostics : MonoBehaviour
    {
        [SerializeField] private WhisperManager _whisper;
        [SerializeField] private VoiceCommandOneShotCapture _capture;

        private void Start()
        {
            if (_whisper == null)
            {
                _whisper = GetComponent<WhisperManager>();
            }

            if (_capture == null)
            {
                _capture = GetComponent<VoiceCommandOneShotCapture>();
            }

            if (_whisper == null || _capture == null)
            {
                Debug.LogWarning("[Voice] Thiếu WhisperManager hoặc VoiceCommandOneShotCapture trên prefab.", this);
                return;
            }

            string modelState = _whisper.IsLoaded
                ? "đã load"
                : _whisper.IsLoading
                    ? "đang load…"
                    : "chưa load";

            GameEventLog.Post(
                $"[Voice] STT sẵn sàng — nhấn V. Model: {_whisper.ModelPath} ({modelState}).",
                GameEventLogCategory.Info);
        }
    }
}
