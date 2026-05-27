namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Ánh xạ chuỗi STT sang id lệnh game (DIP: gameplay không phụ thuộc Vosk).
    /// </summary>
    public interface IVoiceCommandResolver
    {
        bool TryResolve(
            string recognizedText,
            out string commandId,
            out string canonicalPhraseInDatasetForm,
            out float similarity01);

        /// <summary>
        /// Ánh xạ câu (kể cả alias / diễn đạt gần) về PrimaryPhrase mẫu; có thể không đủ ngưỡng lệnh.
        /// </summary>
        bool TryMapToCanonicalPhrase(
            string recognizedText,
            out string commandId,
            out string canonicalPhraseInDatasetForm,
            out float similarity01,
            out bool isCommandMatch);
    }
}
