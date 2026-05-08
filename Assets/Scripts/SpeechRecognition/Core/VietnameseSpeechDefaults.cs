namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Đường dẫn mặc định tới model Vosk tiếng Việt trong StreamingAssets (SRP: một nơi đổi bản model).
    /// </summary>
    public static class VietnameseSpeechDefaults
    {
        /// <summary>Đường dẫn tương đối: thư mục chứa am/conf/graph của vosk-model-vn.</summary>
        public const string DefaultVoskModelRelativePath = "VoskModels/vosk-model-vn-0.4";
    }
}
