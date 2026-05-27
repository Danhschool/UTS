using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Một lệnh thoại: id game + câu chính + alias (OCP: mở rộng bằng asset, không sửa code).
    /// </summary>
    [Serializable]
    public sealed class VoiceCommandEntry
    {
        [Tooltip("ID duy nhất cho gameplay (ví dụ attack, build_barracks).")]
        public string CommandId = "command";

        [Tooltip("Câu ngắn, rõ; nhất quán có/không dấu với alias.")]
        public string PrimaryPhrase = string.Empty;

        [Tooltip("Biến thể (không dấu / viết tắt / đồng nghĩa).")]
        public string[] Aliases = Array.Empty<string>();
    }

    /// <summary>
    /// Tập lệnh đóng + ngưỡng fuzzy + sinh JSON grammar cho Vosk (SRP: dữ liệu lệnh).
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceCommandProfile", menuName = "ProjectRTS/Voice Command Profile")]
    public sealed class VoiceCommandProfile : ScriptableObject
    {
        [Tooltip("Ngưỡng tối thiểu [0..1] sau fuzzy; tăng nếu hay nhận nhầm, giảm nếu hay trượt.")]
        [Range(0.35f, 1f)]
        [SerializeField] private float _minSimilarity = 0.72f;

        [Tooltip("Ngưỡng thấp hơn: câu tương tự vẫn chuyển về PrimaryPhrase mẫu (không kích hoạt lệnh).")]
        [Range(0.35f, 1f)]
        [SerializeField] private float _phraseSnapMinSimilarity = 0.58f;

        [SerializeField] private List<VoiceCommandEntry> _commands = new List<VoiceCommandEntry>();

        public float MinSimilarity => _minSimilarity;
        public float PhraseSnapMinSimilarity => _phraseSnapMinSimilarity;
        public IReadOnlyList<VoiceCommandEntry> Commands => _commands;

        /// <summary>
        /// Mục tiêu: Lấy PrimaryPhrase của lệnh ở dạng dataset (không dấu, chữ thường).
        /// Cách hoạt động: Tìm entry theo CommandId, chuẩn hóa PrimaryPhrase.
        /// </summary>
        public bool TryGetCanonicalPhraseDatasetForm(string commandId, out string canonicalPhrase)
        {
            canonicalPhrase = null;
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            foreach (var entry in _commands)
            {
                if (entry == null || !string.Equals(entry.CommandId, commandId.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                canonicalPhrase = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(entry.PrimaryPhrase);
                return canonicalPhrase.Length > 0;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Nạp toàn bộ lệnh từ file JSON (VoiceCommandDatasetFile) vào profile runtime/editor.
        /// Cách hoạt động: Xóa list cũ, thêm entry hợp lệ; cập nhật minSimilarity nếu trong khoảng cho phép.
        /// </summary>
        public void ImportFromDatasetFile(VoiceCommandDatasetFile data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _commands.Clear();
            if (data.commands != null)
            {
                foreach (var e in data.commands)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.CommandId))
                    {
                        continue;
                    }

                    _commands.Add(e);
                }
            }

            if (data.minSimilarity >= 0.35f && data.minSimilarity <= 1f)
            {
                _minSimilarity = data.minSimilarity;
            }
        }

        /// <summary>
        /// Mục tiêu: Tiện ích một bước: chuỗi JSON → dataset → ImportFromDatasetFile.
        /// </summary>
        public void ImportFromJson(string json)
        {
            var dataset = VoiceCommandDatasetFile.FromJson(json);
            ImportFromDatasetFile(dataset);
        }

        /// <summary>
        /// Chuẩn hóa để so khớp — cùng dạng với mẫu câu trong dataset.
        /// </summary>
        public static string NormalizeForMatch(string raw)
        {
            return RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(raw);
        }

        /// <summary>
        /// Chuẩn hóa cụm cho grammar Vosk.
        /// </summary>
        public static string NormalizeForGrammar(string raw)
        {
            return RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(raw);
        }

        /// <summary>
        /// Sinh JSON mảng chuỗi cho constructor grammar của Vosk.
        /// </summary>
        public string BuildVoskGrammarJson()
        {
            var phrases = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in _commands)
            {
                AddPhrase(phrases, e.PrimaryPhrase);
                if (e.Aliases == null)
                {
                    continue;
                }

                foreach (var a in e.Aliases)
                {
                    AddPhrase(phrases, a);
                }
            }

            if (phrases.Count == 0)
            {
                return "[]";
            }

            var sb = new StringBuilder(phrases.Count * 16);
            sb.Append('[');
            var first = true;
            foreach (var p in phrases)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append('"');
                sb.Append(EscapeJsonString(p));
                sb.Append('"');
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Grammar gọn (chỉ PrimaryPhrase) — dùng khi grammar đầy đủ quá lớn hoặc Vosk báo lỗi.
        /// </summary>
        public string BuildVoskGrammarJsonPrimaryOnly()
        {
            var phrases = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in _commands)
            {
                AddPhrase(phrases, e.PrimaryPhrase);
            }

            if (phrases.Count == 0)
            {
                return "[]";
            }

            var sb = new StringBuilder(phrases.Count * 16);
            sb.Append('[');
            var first = true;
            foreach (var p in phrases)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append('"');
                sb.Append(EscapeJsonString(p));
                sb.Append('"');
            }

            sb.Append(']');
            return sb.ToString();
        }

        public bool HasGrammarPhrases()
        {
            foreach (var e in _commands)
            {
                if (!string.IsNullOrWhiteSpace(e.PrimaryPhrase))
                {
                    return true;
                }

                if (e.Aliases == null)
                {
                    continue;
                }

                foreach (var a in e.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(a))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddPhrase(ISet<string> set, string phrase)
        {
            var n = NormalizeForGrammar(phrase);
            if (!string.IsNullOrEmpty(n))
            {
                set.Add(n);
            }
        }

        private static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
