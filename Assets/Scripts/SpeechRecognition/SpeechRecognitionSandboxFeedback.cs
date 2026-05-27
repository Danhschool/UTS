using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Hiển thị STT và kết quả fuzzy trên Canvas trong scene sandbox (SRP: chỉ UI phản hồi).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeechRecognitionSandboxFeedback : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionBackendBehaviour _backend;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _sttText;
        [SerializeField] private Text _commandText;

        private void OnEnable()
        {
            if (_backend != null)
            {
                _backend.PartialTextUpdated += OnPartialStt;
            }

            SetStatus("Sẵn sàng — nói vào micro (cần model Vosk trong StreamingAssets).");
        }

        private void OnDisable()
        {
            if (_backend != null)
            {
                _backend.PartialTextUpdated -= OnPartialStt;
            }
        }

        public void OnCommandMatched(string commandId)
        {
            if (_commandText != null)
            {
                _commandText.text = $"Lệnh khớp: {commandId}";
                _commandText.color = new Color(0.2f, 0.85f, 0.35f);
            }

            SetStatus("Đã nhận lệnh.");
        }

        public void OnCommandMatchedWithScore(string commandId, float score)
        {
            OnCommandMatched(commandId);
            if (_commandText != null)
            {
                _commandText.text += $"  (score {score:F2})";
            }
        }

        public void OnCommandMatchedWithPhrase(string commandId, string phraseInDatasetForm, float score)
        {
            if (_sttText != null)
            {
                _sttText.text = $"Câu (chuẩn hóa): {phraseInDatasetForm}";
            }

            OnCommandMatchedWithScore(commandId, score);
        }

        public void OnPhraseNormalized(string phraseInDatasetForm)
        {
            if (_sttText != null)
            {
                _sttText.text = $"Câu (chuẩn hóa): {phraseInDatasetForm}";
            }
        }

        public void OnNoCommandMatch(string recognizedText)
        {
            if (_commandText != null)
            {
                _commandText.text = $"Gần mẫu / chưa đủ ngưỡng lệnh: \"{recognizedText}\"";
                _commandText.color = new Color(1f, 0.55f, 0.2f);
            }

            SetStatus($"Chưa kích hoạt lệnh (câu đã chuẩn hóa: \"{recognizedText}\")");
        }

        public void OnPhraseSnappedToSample(string commandId, string canonicalPhrase, float score)
        {
            OnPhraseNormalized(canonicalPhrase);
            if (_commandText != null)
            {
                _commandText.text = $"Gần \"{commandId}\" ({score:F2}) → \"{canonicalPhrase}\"";
                _commandText.color = new Color(0.95f, 0.85f, 0.25f);
            }
        }

        private void OnPartialStt(string text)
        {
            if (_sttText == null)
            {
                return;
            }

            _sttText.text = string.IsNullOrWhiteSpace(text)
                ? "Đang nghe…"
                : $"Đang nghe… {text}";
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }
    }
}
