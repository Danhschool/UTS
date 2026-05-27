namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Đường dẫn model Whisper trong StreamingAssets (SRP: hằng số cấu hình).
    /// </summary>
    public static class WhisperSpeechDefaults
    {
        /// <summary>Model có sẵn trong project (~148 MB).</summary>
        public const string BaseModelRelativePath = "Whisper/ggml-base.bin";

        /// <summary>Khuyên dùng khi tải thêm từ Hugging Face (ggml-small-q8_0.bin).</summary>
        public const string SmallQ8ModelRelativePath = "Whisper/ggml-small-q8_0.bin";

        public const string DefaultLanguage = "vi";

        /// <summary>Cân bằng (prefab cũ).</summary>
        public const float DefaultStreamStepSec = 1.5f;

        /// <summary>Ưu tiên phản hồi nhanh — xem WhisperStreamingTuning.</summary>
        public const float LowLatencyStreamStepSec = WhisperStreamingTuning.LowLatencyStepSec;
    }
}
