using System.Text;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// Start: đọc JSON và gán rich text (in đậm theo từng dòng) vào TextMeshProUGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManualScrollViewBinder : MonoBehaviour
    {
        const string DefaultResourcePath = "UI/game_manual_vi";

        [SerializeField] TextMeshProUGUI targetText;
        [SerializeField] string resourcePath = DefaultResourcePath;

        void Start()
        {
            FillFromResources();
        }

        /// <summary>
        /// Mục tiêu: hiển thị nội dung hướng dẫn từ JSON lên TMP.
        /// Cách hoạt động: parse lines[] — bold=true bọc thẻ &lt;b&gt; cho TextMeshPro.
        /// </summary>
        public void FillFromResources()
        {
            if (targetText == null)
            {
                targetText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (targetText == null)
            {
                Debug.LogWarning($"{nameof(GameManualScrollViewBinder)}: missing TextMeshProUGUI.", this);
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"{nameof(GameManualScrollViewBinder)}: cannot load '{resourcePath}'.", this);
                return;
            }

            GameManualDocument document = JsonUtility.FromJson<GameManualDocument>(asset.text);
            if (document?.lines == null || document.lines.Length == 0)
            {
                Debug.LogWarning($"{nameof(GameManualScrollViewBinder)}: JSON has no lines.", this);
                return;
            }

            targetText.richText = true;
            targetText.text = BuildRichText(document.lines);
        }

        static string BuildRichText(GameManualLine[] lines)
        {
            var builder = new StringBuilder(4096);

            for (int i = 0; i < lines.Length; i++)
            {
                GameManualLine line = lines[i];
                if (string.IsNullOrWhiteSpace(line.text))
                {
                    continue;
                }

                if (line.bold)
                {
                    builder.Append("<b>").Append(line.text.Trim()).Append("</b>");
                }
                else
                {
                    builder.Append(line.text.Trim());
                }

                if (i < lines.Length - 1)
                {
                    builder.Append("\n\n");
                }
            }

            return builder.ToString();
        }

        [System.Serializable]
        public class GameManualDocument
        {
            public GameManualLine[] lines;
        }

        [System.Serializable]
        public class GameManualLine
        {
            public string text;
            public bool bold;
        }
    }
}
