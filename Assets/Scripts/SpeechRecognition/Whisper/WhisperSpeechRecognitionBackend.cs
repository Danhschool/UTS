using System.Threading.Tasks;
using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;
using Whisper;
using Whisper.Utils;

namespace ProjectRTS.SpeechRecognition.Whisper
{
    /// <summary>
    /// Adapter Whisper streaming (theo mẫu StreamingSampleMic) → ISpeechRecognitionBackend (DIP).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class WhisperSpeechRecognitionBackend : SpeechRecognitionBackendBehaviour
    {
        [SerializeField] private WhisperManager _whisperManager;
        [SerializeField] private MicrophoneRecord _microphoneRecord;
        [SerializeField] private VoiceCommandProfile _commandProfileForPrompt;

        [Tooltip("Tự bật micro + stream khi model đã load (dùng khi chơi game).")]
        [SerializeField] private bool _startListeningOnPlay = true;

        [Tooltip("Ghi đè stepSec trên WhisperManager khi khởi động (giây).")]
        [SerializeField] private float _streamStepSec = WhisperSpeechDefaults.DefaultStreamStepSec;

        private WhisperStream _stream;
        private bool _listening;

        private async void Start()
        {
            if (_startListeningOnPlay)
            {
                await StartListeningAsync();
            }
        }

        private void OnDestroy()
        {
            StopListening();
            UnhookStream();
        }

        public override void Initialize()
        {
        }

        public override void AppendPcm16(short[] buffer, int sampleCount)
        {
        }

        public override void Flush()
        {
            StopListening();
        }

        /// <summary>
        /// Mục tiêu: Bật nhận diện realtime (micro + WhisperStream).
        /// Cách hoạt động: Chờ model load → CreateStream → đăng ký event → StartStream + StartRecord.
        /// </summary>
        public async Task StartListeningAsync()
        {
            if (_listening)
            {
                return;
            }

            if (!ResolveDependencies())
            {
                return;
            }

            ApplyWhisperSettingsFromProfile();
            await WaitForModelLoadedAsync();

            UnhookStream();
            _stream = await _whisperManager.CreateStream(_microphoneRecord);
            if (_stream == null)
            {
                Debug.LogError("[Whisper] CreateStream failed.", this);
                return;
            }

            _stream.OnResultUpdated += OnStreamResultUpdated;
            _stream.OnSegmentFinished += OnStreamSegmentFinished;

            _stream.StartStream();
            _microphoneRecord.StartRecord();
            _listening = true;
            Debug.Log("[Whisper] Đang nghe micro (streaming).", this);
        }

        /// <summary>
        /// Mục tiêu: Dừng stream/micro khi thoát scene hoặc tắt voice.
        /// Cách hoạt động: StopRecord → WhisperStream.OnStreamFinished qua MicrophoneRecord.
        /// </summary>
        public void StopListening()
        {
            if (!_listening || _microphoneRecord == null)
            {
                return;
            }

            if (_microphoneRecord.IsRecording)
            {
                _microphoneRecord.StopRecord();
            }

            _listening = false;
        }

        private bool ResolveDependencies()
        {
            if (_whisperManager == null)
            {
                _whisperManager = GetComponent<WhisperManager>();
            }

            if (_microphoneRecord == null)
            {
                _microphoneRecord = GetComponent<MicrophoneRecord>();
            }

            if (_whisperManager == null || _microphoneRecord == null)
            {
                Debug.LogError(
                    $"{nameof(WhisperSpeechRecognitionBackend)} cần {nameof(WhisperManager)} và {nameof(MicrophoneRecord)} trên cùng GameObject.",
                    this);
                return false;
            }

            return true;
        }

        private async Task WaitForModelLoadedAsync()
        {
            if (_whisperManager.IsLoaded)
            {
                return;
            }

            if (!_whisperManager.IsLoading)
            {
                await _whisperManager.InitModel();
            }

            while (_whisperManager.IsLoading)
            {
                await Task.Yield();
            }

            if (!_whisperManager.IsLoaded)
            {
                Debug.LogError(
                    $"[Whisper] Model chưa load. Đặt file .bin vào StreamingAssets/{WhisperSpeechDefaults.BaseModelRelativePath}",
                    this);
            }
        }

        private void ApplyWhisperSettingsFromProfile()
        {
            _whisperManager.language = WhisperSpeechDefaults.DefaultLanguage;
            _whisperManager.translateToEnglish = false;
            _whisperManager.stepSec = _streamStepSec;
            _whisperManager.useVad = true;

            if (_commandProfileForPrompt != null)
            {
                var prompt = WhisperVoicePromptBuilder.BuildFromProfile(_commandProfileForPrompt);
                if (!string.IsNullOrEmpty(prompt))
                {
                    _whisperManager.initialPrompt = prompt;
                }
            }
        }

        private void OnStreamResultUpdated(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                RaisePartial(text.Trim());
            }
        }

        private void OnStreamSegmentFinished(WhisperResult segment)
        {
            if (segment == null || string.IsNullOrWhiteSpace(segment.Result))
            {
                return;
            }

            RaiseFinal(segment.Result.Trim());
        }

        private void UnhookStream()
        {
            if (_stream == null)
            {
                return;
            }

            _stream.OnResultUpdated -= OnStreamResultUpdated;
            _stream.OnSegmentFinished -= OnStreamSegmentFinished;
            _stream = null;
        }
    }
}
