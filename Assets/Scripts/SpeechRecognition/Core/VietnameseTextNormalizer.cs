using System.Globalization;
using System.Text;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Chuẩn hóa chuỗi tiếng Việt cho STT / fuzzy / grammar Vosk (SRP: chỉ xử lý text).
    /// </summary>
    public static class VietnameseTextNormalizer
    {
        /// <summary>
        /// Mục tiêu: Bỏ dấu tiếng Việt để so khớp "dừng lại" với "dung lai".
        /// Cách hoạt động: FormD tách dấu → lọc NonSpacingMark → ghép lại, xử lý đ/Đ.
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'd');
        }

        /// <summary>
        /// Mục tiêu: Loại dấu câu khỏi cụm grammar / so khớp.
        /// Cách hoạt động: Giữ chữ, số, khoảng trắng; gộp khoảng trắng thừa.
        /// </summary>
        public static string StripPunctuation(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            var pendingSpace = false;
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (pendingSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    pendingSpace = false;
                    sb.Append(ch);
                }
                else if (char.IsWhiteSpace(ch))
                {
                    pendingSpace = sb.Length > 0;
                }
            }

            return sb.ToString();
        }
    }
}
