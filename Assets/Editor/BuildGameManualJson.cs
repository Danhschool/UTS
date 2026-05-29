#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    public static class BuildGameManualJson
    {
        const string PoPath = @"d:\Unity_3D\0AD_Public\l10n\vi.public-gui-manual.po";
        const string OutPath = "Assets/Resources/UI/game_manual_vi.json";

        static readonly HashSet<string> BoldSectionTitles = new()
        {
            "Cài đặt đồ họa",
            "Điều khiển trong game",
            "Các điều khiển cơ bản (mặc định):",
            "Chế độ chơi",
            "Thiết lập trò chơi",
            "Phím tắt",
            "Toàn chương trình",
            "Trong trò chơi",
            "Sửa đổi thao tác chuột",
            "Lớp phủ",
            "Thao tác camera",
            "Trong quá trình đặt công trình",
            "Khi tải trò chơi đã lưu"
        };

        [MenuItem("ProjectRTS/UI/Rebuild Game Manual JSON (vi, with bold)")]
        public static void Rebuild()
        {
            if (!File.Exists(PoPath))
            {
                Debug.LogError($"Missing: {PoPath}");
                return;
            }

            var lines = new List<LineDto>();
            string po = File.ReadAllText(PoPath);
            foreach (string block in Regex.Split(po, @"\n\n+"))
            {
                Match refMatch = Regex.Match(block, @"gui/manual/intro\.txt:(\d+)");
                if (!refMatch.Success)
                {
                    continue;
                }

                string text = StripFontTags(ParseMsgstr(block));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                bool bold = refMatch.Groups[1].Value == "1"
                    || BoldSectionTitles.Contains(text)
                    || IsSubheading(text);

                lines.Add(new LineDto { text = text, bold = bold });
            }

            string json = JsonUtility.ToJson(new DocumentDto { lines = lines.ToArray() }, true);
            string fullPath = Path.Combine(Application.dataPath, "Resources/UI/game_manual_vi.json");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? "Assets/Resources/UI");
            File.WriteAllText(fullPath, json);
            AssetDatabase.Refresh();
            Debug.Log($"Wrote {OutPath} ({lines.Count} lines)");
        }

        static bool IsSubheading(string text)
        {
            if (text.Length > 64 || text.StartsWith("•") || text.StartsWith("hotkey."))
            {
                return false;
            }

            return text.EndsWith(":") && !text.Contains(" – ");
        }

        static string ParseMsgstr(string block)
        {
            int idx = block.IndexOf("msgstr ");
            if (idx < 0)
            {
                return string.Empty;
            }

            string raw = block.Substring(idx + 7).Trim();
            if (raw.StartsWith("\"\"", System.StringComparison.Ordinal))
            {
                var parts = new System.Text.StringBuilder();
                foreach (string line in raw.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length >= 2 && t.StartsWith("\"") && t.EndsWith("\""))
                    {
                        parts.Append(t.Substring(1, t.Length - 2));
                    }
                }

                return parts.ToString().Replace("\\n", "\n");
            }

            return raw.Trim('"').Replace("\\n", "\n");
        }

        static string StripFontTags(string text)
        {
            text = Regex.Replace(text, @"\[font=\\?""[^""]+\\?""\]", string.Empty);
            text = Regex.Replace(text, @"\[font=""[^""]+""\]", string.Empty);
            return text.Replace('\u00a0', ' ').Trim();
        }

        [System.Serializable]
        class DocumentDto
        {
            public LineDto[] lines;
        }

        [System.Serializable]
        class LineDto
        {
            public string text;
            public bool bold;
        }
    }
}
#endif
