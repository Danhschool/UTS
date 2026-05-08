using System.Text.RegularExpressions;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Parses Vosk JSON snippets without pulling Json.NET (SRP: text extraction only).
    /// </summary>
    public static class VoskJsonTextExtractor
    {
        private static readonly Regex s_partial = new Regex("\"partial\"\\s*:\\s*\"([^\"]*)\"",

            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex s_text = new Regex("\"text\"\\s*:\\s*\"([^\"]*)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryGetPartial(string json, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var m = s_partial.Match(json);
            if (!m.Success)
            {
                return false;
            }

            text = m.Groups[1].Value;
            return true;
        }

        public static bool TryGetText(string json, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var m = s_text.Match(json);
            if (!m.Success)
            {
                return false;
            }

            text = m.Groups[1].Value;
            return true;
        }
    }
}
