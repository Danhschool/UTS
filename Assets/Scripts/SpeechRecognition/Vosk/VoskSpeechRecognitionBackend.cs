using System;
using System.IO;
using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Vosk
{
    /// <summary>
    /// Backend Vosk: nạp model, đưa PCM16 16 kHz vào recognizer, phát sự kiện partial/final (SRP: chỉ nhận dạng Vosk).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoskSpeechRecognitionBackend : SpeechRecognitionBackendBehaviour
    {
        [Tooltip("Đường dẫn tương đối trong StreamingAssets tới thư mục model tiếng Việt (ví dụ: VoskModels/vosk-model-vn-0.4). Chỉ dùng model vosk-model-vn-* để nhận diện tiếng Việt.")]
        [SerializeField] private string _modelRelativePath = VietnameseSpeechDefaults.DefaultVoskModelRelativePath;

        [Tooltip("Bật: chỉ cho phép đường dẫn có dấu hiệu model VN (vosk-model-vn). Tắt nếu bạn đặt tên thư mục khác hoàn toàn.")]
        [SerializeField] private bool _enforceVietnameseModelFolder = true;

        [SerializeField] private bool _logPartialToConsole;

        [Tooltip("Trùng VoiceCommandProfile với VoiceCommandRouter. Có PrimaryPhrase/Aliases → VoskRecognizer(..., grammarJson) chỉ tìm trong các cụm đó → chính xác hơn free-form. Để trống = nhận dạng tự do.")]
        [SerializeField] private VoiceCommandProfile _commandProfile;

        [Tooltip("Bật: trong Initialize(), nạp JSON lệnh từ Resources vào Command Profile (cần gán Profile). Dùng khi scene chỉ có Vosk + micro, không gắn VoiceCommandRouter. Nếu Router đã bật Load Dataset trên cùng Profile thì tắt cái này để tránh nạp hai lần.")]
        [SerializeField] private bool _importDatasetFromResourcesBeforeInit;

        [Tooltip("Resources.Load không gồm .json — ví dụ VoiceCommands/rts_voice_commands_standard_vi")]
        [SerializeField] private string _resourcesDatasetPath = "VoiceCommands/rts_voice_commands_standard_vi";

        private global::Vosk.Model _model;
        private global::Vosk.VoskRecognizer _recognizer;
        private bool _initialized;

        public override void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            ApplyResourcesDatasetIfRequested();

            global::Vosk.Vosk.SetLogLevel(0);
            var relative = NormalizeModelPathRelativeToStreamingAssets(_modelRelativePath);
            var streamingRoot = Application.streamingAssetsPath;
            if (string.IsNullOrWhiteSpace(streamingRoot))
            {
                streamingRoot = Path.Combine(Application.dataPath, "StreamingAssets");
            }

            var modelPath = Path.Combine(streamingRoot, relative);

            if (string.IsNullOrEmpty(relative))
            {
                Debug.LogError("Vosk model path is empty after normalization. Nhập đường dẫn tương đối trong StreamingAssets, ví dụ: VoskModels/vosk-model-vn-0.4 (không bắt đầu bằng \\ hoặc Assets/StreamingAssets/).", this);
                return;
            }

            if (_enforceVietnameseModelFolder && !IsLikelyVietnameseVoskModelPath(relative))
            {
                Debug.LogError(
                    $"Chỉ nhận diện tiếng Việt: đường dẫn model phải là bản vosk-model-vn (đường dẫn hiện tại: '{relative}'). Đặt lại hoặc tắt '{nameof(_enforceVietnameseModelFolder)}'.",
                    this);
                return;
            }

            if (!Directory.Exists(modelPath))
            {
                Debug.LogError($"Vosk model folder not found: {modelPath}. Tải model từ trang Vosk và giải nén vào StreamingAssets/{relative}.", this);
                return;
            }

            try
            {
                _model = new global::Vosk.Model(modelPath);
                _recognizer = CreateRecognizerOrFallback(modelPath);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.SetWords(false);
                _initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        public override void AppendPcm16(short[] buffer, int sampleCount)
        {
            if (!_initialized || _recognizer == null || buffer == null || sampleCount <= 0)
            {
                return;
            }

            if (_recognizer.AcceptWaveform(buffer, sampleCount))
            {
                var json = _recognizer.Result();
                if (VoskJsonTextExtractor.TryGetText(json, out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    RaiseFinal(text.Trim());
                }
            }
            else
            {
                var json = _recognizer.PartialResult();
                if (VoskJsonTextExtractor.TryGetPartial(json, out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    if (_logPartialToConsole)
                    {
                        Debug.Log($"[Vosk partial] {text}");
                    }

                    RaisePartial(text.Trim());
                }
            }
        }

        public override void Flush()
        {
            if (_recognizer == null)
            {
                return;
            }

            var json = _recognizer.FinalResult();
            if (VoskJsonTextExtractor.TryGetText(json, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                RaiseFinal(text.Trim());
            }
        }

        /// <summary>
        /// Mục tiêu: Cho phép dùng file JSON trong Resources làm grammar mà không cần VoiceCommandRouter.
        /// Cách hoạt động: Nếu bật cờ và đã có Command Profile thì VoiceCommandDatasetFile.LoadFromResources rồi ImportFromDatasetFile trước khi dựng recognizer có grammar.
        /// </summary>
        private void ApplyResourcesDatasetIfRequested()
        {
            if (!_importDatasetFromResourcesBeforeInit || _commandProfile == null)
            {
                return;
            }

            var dataset = VoiceCommandDatasetFile.LoadFromResources(_resourcesDatasetPath);
            _commandProfile.ImportFromDatasetFile(dataset);
        }

        private void OnDestroy()
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _model?.Dispose();
            _model = null;
            _initialized = false;
        }

        /// <summary>
        /// Tạo recognizer có grammar nếu profile có cụm; nếu grammar lỗi thì fallback không grammar (OCP: mở rộng bằng profile).
        /// </summary>
        private global::Vosk.VoskRecognizer CreateRecognizerOrFallback(string modelPathForLog)
        {
            if (_commandProfile != null && _commandProfile.HasGrammarPhrases())
            {
                var grammarJson = _commandProfile.BuildVoskGrammarJson();
                if (grammarJson != null && grammarJson != "[]")
                {
                    try
                    {
                        return new global::Vosk.VoskRecognizer(_model, 16000f, grammarJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Vosk grammar không hợp lệ hoặc không được model hỗ trợ; dùng recognizer không grammar. Model: {modelPathForLog}. Chi tiết: {ex.Message}",
                            this);
                    }
                }
            }

            return new global::Vosk.VoskRecognizer(_model, 16000f);
        }

        /// <summary>
        /// Kiểm tra đường dẫn có trỏ tới bản model Vosk tiếng Việt (thư mục thường chứa vosk-model-vn).
        /// </summary>
        private static bool IsLikelyVietnameseVoskModelPath(string relativeNormalized)
        {
            if (string.IsNullOrEmpty(relativeNormalized))
            {
                return false;
            }

            var r = relativeNormalized.Replace('\\', '/');
            return r.IndexOf("vosk-model-vn", StringComparison.OrdinalIgnoreCase) >= 0
                   || r.IndexOf("model-vn", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Chuẩn hóa chuỗi người dùng nhập: bỏ dấu \\ hoặc / đầu (tránh Path.Combine trên Windows bỏ qua StreamingAssets),
        /// bỏ tiền tố Assets/StreamingAssets/ nếu dán nhầm từ Explorer.
        /// </summary>
        private static string NormalizeModelPathRelativeToStreamingAssets(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var s = raw.Trim().Replace('\\', '/');
            while (s.StartsWith("/", StringComparison.Ordinal))
            {
                s = s.Length > 1 ? s.Substring(1) : string.Empty;
            }

            while (s.StartsWith("./", StringComparison.Ordinal))
            {
                s = s.Length > 2 ? s.Substring(2) : string.Empty;
            }

            const string prefix = "assets/streamingassets/";
            while (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                s = s.Length > prefix.Length ? s.Substring(prefix.Length) : string.Empty;
                while (s.StartsWith("/", StringComparison.Ordinal))
                {
                    s = s.Length > 1 ? s.Substring(1) : string.Empty;
                }
            }

            return s;
        }
    }
}
