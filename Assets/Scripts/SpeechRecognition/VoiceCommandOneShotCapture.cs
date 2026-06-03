using System;
using System.Collections;
using GameDevTV.RTS.UI.GameEventLog;
using ProjectRTS.SpeechRecognition.Core;
using ProjectRTS.SpeechRecognition.Whisper;
using UnityEngine;
using Whisper;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Thu một câu STT (Whisper) → đăng transcript lên chat — không ánh xạ lệnh game.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class VoiceCommandOneShotCapture : MonoBehaviour
    {
        [SerializeField] private WhisperSttOnlyDriver _sttDriver;
        [SerializeField] private WhisperManager _whisper;

        [Tooltip("Thời gian tối đa mỗi lần nhấn V / nút (giây).")]
        [SerializeField] private float _maxCaptureSeconds = 12f;

        private Coroutine _timeoutRoutine;
        private bool _isCapturing;

        public bool IsCapturing => _isCapturing;
        public string LastTranscript { get; private set; }

        public event Action<string> TranscriptCommitted;

        private void Awake()
        {
            if (_sttDriver == null)
            {
                _sttDriver = GetComponent<WhisperSttOnlyDriver>();
            }

            if (_whisper == null)
            {
                _whisper = GetComponent<WhisperManager>();
            }

            EnsureCompanionComponents();
        }

        private void EnsureCompanionComponents()
        {
            if (GetComponent<VoiceCommandPushToTalkInput>() == null)
            {
                gameObject.AddComponent<VoiceCommandPushToTalkInput>();
            }

            if (GetComponent<WhisperSttRecordControls>() == null)
            {
                gameObject.AddComponent<WhisperSttRecordControls>();
            }

            if (GetComponent<VoiceCommandRuntimeDiagnostics>() == null)
            {
                gameObject.AddComponent<VoiceCommandRuntimeDiagnostics>();
            }

            if (GetComponent<VoiceCommandOneShotTranscriptMapper>() == null)
            {
                gameObject.AddComponent<VoiceCommandOneShotTranscriptMapper>();
            }

            if (GetComponent<VoiceCommandGameplayExecutor>() == null)
            {
                gameObject.AddComponent<VoiceCommandGameplayExecutor>();
            }
        }

        private void OnEnable()
        {
            if (_sttDriver != null)
            {
                _sttDriver.SegmentTranscriptFinalized += OnSegmentFinalized;
                _sttDriver.ListeningStateChanged += OnListeningStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_sttDriver != null)
            {
                _sttDriver.SegmentTranscriptFinalized -= OnSegmentFinalized;
                _sttDriver.ListeningStateChanged -= OnListeningStateChanged;
            }

            CancelCaptureInternal();
        }

        /// <summary>
        /// Mục tiêu: Bắt đầu phiên thu (phím V hoặc nút UI).
        /// Cách hoạt động: Đợi model Whisper → bật micro → hẹn giờ tối đa.
        /// </summary>
        public void BeginCapture()
        {
            if (_isCapturing || _sttDriver == null)
            {
                return;
            }

            if (_timeoutRoutine != null)
            {
                StopCoroutine(_timeoutRoutine);
            }

            _timeoutRoutine = StartCoroutine(BeginCaptureRoutine());
        }

        public void CancelCapture()
        {
            CancelCaptureInternal();
        }

        private IEnumerator BeginCaptureRoutine()
        {
            if (_whisper != null)
            {
                if (!_whisper.IsLoaded && !_whisper.IsLoading)
                {
                    _ = _whisper.InitModel();
                }

                float waitDeadline = Time.realtimeSinceStartup + 30f;
                while (_whisper.IsLoading && Time.realtimeSinceStartup < waitDeadline)
                {
                    yield return null;
                }

                if (!_whisper.IsLoaded)
                {
                    GameEventLog.Post(
                        "[Voice] Whisper chưa load xong — đợi vài giây rồi thử lại.",
                        GameEventLogCategory.Warning);
                    _timeoutRoutine = null;
                    yield break;
                }
            }

            _isCapturing = true;
            GameEventLog.Post("[Voice] Đang nghe…", GameEventLogCategory.Info);
            _ = _sttDriver.StartListeningAsync();
            yield return CaptureTimeoutRoutine();
        }

        private void OnSegmentFinalized(string transcript)
        {
            if (!_isCapturing || string.IsNullOrWhiteSpace(transcript))
            {
                return;
            }

            FinishCapture(transcript.Trim());
        }

        private IEnumerator CaptureTimeoutRoutine()
        {
            yield return new WaitForSeconds(_maxCaptureSeconds);
            if (_isCapturing)
            {
                FinishCapture(string.Empty);
            }

            _timeoutRoutine = null;
        }

        /// <summary>
        /// Mục tiêu: Kết thúc thu và đưa câu nhận diện lên khung chat.
        /// Cách hoạt động: Dừng micro → GameEventLog.Post → event TranscriptCommitted.
        /// </summary>
        private void FinishCapture(string transcript)
        {
            LastTranscript = transcript ?? string.Empty;
            CancelCaptureInternal();

            if (LastTranscript.Length > 0)
            {
                GameEventLog.Post($"[Voice] {LastTranscript}", GameEventLogCategory.Info);
                Debug.Log($"[Voice STT] {LastTranscript}", this);
            }
            else
            {
                GameEventLog.Post("[Voice] Không nghe được câu nào.", GameEventLogCategory.Warning);
            }

            TranscriptCommitted?.Invoke(LastTranscript);
        }

        private void CancelCaptureInternal()
        {
            if (_timeoutRoutine != null)
            {
                StopCoroutine(_timeoutRoutine);
                _timeoutRoutine = null;
            }

            if (_sttDriver != null && _sttDriver.IsListening)
            {
                _sttDriver.StopListening();
            }

            _isCapturing = false;
        }

        private void OnListeningStateChanged(bool listening)
        {
            if (!listening)
            {
                _isCapturing = false;
            }
        }
    }
}
