using System;
using System.Threading.Tasks;
using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

namespace ProjectRTS.SpeechRecognition.Whisper
{
    public enum WhisperSttLatencyProfile
    {
        /// <summary>Giữ giá trị trên WhisperManager / MicrophoneRecord trong Inspector.</summary>
        Inspector = 0,
        Balanced = 1,
        /// <summary>Ưu tiên phản hồi ~1–3s (tùy CPU/GPU).</summary>
        LowLatency = 2,
    }

    /// <summary>
    /// Chỉ nhận diện giọng nói (STT) — copy luồng StreamingSampleMic từ whisper.unity (SRP: không map lệnh game).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WhisperSttOnlyDriver : MonoBehaviour
    {
        [SerializeField] private WhisperManager _whisper;
        [SerializeField] private MicrophoneRecord _microphoneRecord;
        [SerializeField] private Text _transcriptText;

        [Tooltip("LowLatency: stepSec≈1, lengthSec≈5 — nhanh hơn, có thể kém ổn định hơn Balanced.")]
        [SerializeField] private WhisperSttLatencyProfile _latencyProfile = WhisperSttLatencyProfile.LowLatency;

        [Tooltip("Play → tự bật micro + stream. Tự tắt khi có WhisperSttRecordControls.")]
        [SerializeField] private bool _autoStartOnPlay = true;

        [Tooltip("Tự thêm WhisperSttRecordControls + nút bật/tắt nếu prefab chưa gắn.")]
        [SerializeField] private bool _ensureRecordControls = true;

        private WhisperStream _stream;
        private bool _streamReady;
        private bool _isListening;

        public bool IsListening => _isListening;
        public bool IsStreamReady => _streamReady;

        public event Action<bool> ListeningStateChanged;

        /// <summary>Đoạn Whisper hoàn tất (dùng cho ánh xạ lệnh một lần).</summary>
        public event Action<string> SegmentTranscriptFinalized;

        public void AssignTranscriptText(Text text)
        {
            _transcriptText = text;
        }

        private void Awake()
        {
            var commandCapture = GetComponent<ProjectRTS.SpeechRecognition.VoiceCommandOneShotCapture>();
            if (commandCapture != null)
            {
                _ensureRecordControls = false;
                _autoStartOnPlay = false;
            }
            else if (_ensureRecordControls && GetComponent<WhisperSttRecordControls>() == null)
            {
                gameObject.AddComponent<WhisperSttRecordControls>();
            }

            ApplyLatencyProfile();
        }

        private void ApplyLatencyProfile()
        {
            if (!ResolveDependencies())
            {
                return;
            }

            switch (_latencyProfile)
            {
                case WhisperSttLatencyProfile.Balanced:
                    WhisperStreamingTuning.ApplyBalanced(_whisper, _microphoneRecord);
                    break;
                case WhisperSttLatencyProfile.LowLatency:
                    WhisperStreamingTuning.ApplyLowLatency(_whisper, _microphoneRecord);
                    break;
            }
        }

        /// <summary>
        /// Mục tiêu: Gọi từ nút UI / script khác (UnityEvent không hỗ trợ Task).
        /// Cách hoạt động: Đang thu → StopListening; chưa thu → StartListeningAsync.
        /// </summary>
        public void ToggleListening()
        {
            if (_isListening)
            {
                StopListening();
            }
            else
            {
                _ = StartListeningAsync();
            }
        }

        /// <summary>
        /// Mục tiêu: Bật thu từ Inspector (Button OnClick) hoặc gameplay.
        /// Cách hoạt động: Gọi StartListeningAsync nếu chưa đang nghe.
        /// </summary>
        public void StartListening()
        {
            if (!_isListening)
            {
                _ = StartListeningAsync();
            }
        }

        private async void Start()
        {
            if (GetComponent<WhisperSttRecordControls>() != null)
            {
                _autoStartOnPlay = false;
            }

            await InitializeStreamAsync();

            if (_autoStartOnPlay && _streamReady)
            {
                await StartListeningAsync();
            }
            else if (_streamReady)
            {
                SetTranscript("Nhấn «Bắt đầu thu» để nghe micro.");
            }
        }

        private void OnDestroy()
        {
            StopListening();
            UnhookStream();
        }

        /// <summary>
        /// Mục tiêu: Chuẩn bị WhisperStream (không bật micro).
        /// Cách hoạt động: Load model → CreateStream → đăng ký callback transcript.
        /// </summary>
        public async Task InitializeStreamAsync()
        {
            if (_streamReady)
            {
                return;
            }

            if (!ResolveDependencies())
            {
                return;
            }

            _microphoneRecord.echo = false;

            _stream = await _whisper.CreateStream(_microphoneRecord);
            if (_stream == null)
            {
                SetTranscript("Lỗi: không tạo được stream — kiểm tra model trong StreamingAssets/Whisper/.");
                Debug.LogError("[Whisper STT] CreateStream thất bại — kiểm tra model trong StreamingAssets/Whisper/.", this);
                return;
            }

            _stream.OnResultUpdated += OnTranscriptUpdated;
            _stream.OnSegmentFinished += OnSegmentFinished;
            _streamReady = true;
        }

        /// <summary>
        /// Mục tiêu: Bật thu âm + nhận diện realtime.
        /// Cách hoạt động: Khởi tạo stream nếu cần → StartStream + StartRecord.
        /// </summary>
        public async Task StartListeningAsync()
        {
            if (_isListening)
            {
                return;
            }

            if (!_streamReady)
            {
                await InitializeStreamAsync();
            }

            if (_stream == null)
            {
                return;
            }

            _stream.StartStream();
            _microphoneRecord.StartRecord();
            SetListening(true);
            SetTranscript("Đang nghe… nói vào micro.");
            Debug.Log("[Whisper STT] Bắt đầu thu.", this);
        }

        /// <summary>
        /// Mục tiêu: Dừng thu micro (người chơi chủ động tắt STT).
        /// Cách hoạt động: StopRecord khi đang ghi → cập nhật trạng thái UI.
        /// </summary>
        public void StopListening()
        {
            if (!_isListening || _microphoneRecord == null)
            {
                return;
            }

            if (_microphoneRecord.IsRecording)
            {
                _microphoneRecord.StopRecord();
            }

            SetListening(false);
            SetTranscript("Đã dừng thu. Nhấn «Bắt đầu thu» để nghe lại.");
            Debug.Log("[Whisper STT] Đã dừng thu.", this);
        }

        private void SetListening(bool listening)
        {
            if (_isListening == listening)
            {
                return;
            }

            _isListening = listening;
            ListeningStateChanged?.Invoke(_isListening);
        }

        private void UnhookStream()
        {
            if (_stream == null)
            {
                return;
            }

            _stream.OnResultUpdated -= OnTranscriptUpdated;
            _stream.OnSegmentFinished -= OnSegmentFinished;
            _stream = null;
            _streamReady = false;
        }

        private bool ResolveDependencies()
        {
            if (_whisper == null)
            {
                _whisper = GetComponent<WhisperManager>();
            }

            if (_microphoneRecord == null)
            {
                _microphoneRecord = GetComponent<MicrophoneRecord>();
            }

            if (_whisper == null || _microphoneRecord == null)
            {
                Debug.LogError(
                    $"{nameof(WhisperSttOnlyDriver)} cần {nameof(WhisperManager)} và {nameof(MicrophoneRecord)} trên cùng GameObject.",
                    this);
                return false;
            }

            return true;
        }

        private void OnTranscriptUpdated(string result)
        {
            SetTranscript(result);
        }

        private void OnSegmentFinished(WhisperResult segment)
        {
            if (segment == null || string.IsNullOrWhiteSpace(segment.Result))
            {
                return;
            }

            var text = segment.Result.Trim();
            SegmentTranscriptFinalized?.Invoke(text);
            Debug.Log($"[Whisper STT] {text}");
        }

        private void SetTranscript(string text)
        {
            if (_transcriptText != null)
            {
                _transcriptText.text = text;
            }
        }
    }
}
