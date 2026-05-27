using System.Collections.Generic;
using System.Text;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Sinh initial prompt cho Whisper từ tập lệnh RTS (SRP: chỉ build prompt).
    /// </summary>
    public static class WhisperVoicePromptBuilder
    {
        /// <summary>
        /// Mục tiêu: Gợi ý decoder Whisper các cụm lệnh game (không dấu).
        /// Cách hoạt động: Lấy PrimaryPhrase từ profile, chuẩn hóa, nối bằng dấu phẩy, giới hạn độ dài.
        /// </summary>
        public static string BuildFromProfile(VoiceCommandProfile profile, int maxPhrases = 48)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            var phrases = new List<string>();
            foreach (var entry in profile.Commands)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.PrimaryPhrase))
                {
                    continue;
                }

                var n = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(entry.PrimaryPhrase);
                if (n.Length == 0 || phrases.Contains(n))
                {
                    continue;
                }

                phrases.Add(n);
                if (phrases.Count >= maxPhrases)
                {
                    break;
                }
            }

            if (phrases.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(phrases.Count * 24);
            for (var i = 0; i < phrases.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(phrases[i]);
            }

            return sb.ToString();
        }
    }
}
