using System.Text.RegularExpressions;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Đưa text STT về cùng dạng với mẫu câu trong dataset (không dấu, không dấu câu, chữ thường).
    /// </summary>
    public static class RecognizedSpeechPhraseNormalizer
    {
        /// <summary>
        /// Mục tiêu: Chuẩn hóa câu Vosk/người nói để so khớp với PrimaryPhrase/Aliases.
        /// Cách hoạt động: Bỏ dấu câu → bỏ dấu tiếng Việt → chữ thường → gộp khoảng trắng.
        /// </summary>
        public static string ToDatasetPhraseForm(string recognizedSpeech)
        {
            if (string.IsNullOrWhiteSpace(recognizedSpeech))
            {
                return string.Empty;
            }

            var stripped = VietnameseTextNormalizer.StripPunctuation(recognizedSpeech);
            var noMarks = VietnameseTextNormalizer.RemoveDiacritics(stripped);
            return Regex.Replace(noMarks.Trim().ToLowerInvariant(), @"\s+", " ");
        }
    }
}
