using System;
using GameDevTV.RTS.UI.GameEventLog;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Nối transcript one-shot sang command id theo dataset JSON trong Resources.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class VoiceCommandOneShotTranscriptMapper : MonoBehaviour
    {
        [SerializeField] private VoiceCommandOneShotCapture _capture;
        [SerializeField] private VoiceCommandProfile _profile;

        [Tooltip("Nạp dataset JSON ở Awake (không cần đuôi .json).")]
        [SerializeField] private bool _loadDatasetFromResourcesOnAwake = true;

        [SerializeField] private string _resourcesDatasetPath = "VoiceCommands/rts_voice_commands_uts_units_vi";

        [SerializeField] private UnityEvent<string> _onCommandMatched;
        [SerializeField] private UnityEvent<string, float> _onCommandMatchedWithScore;
        [SerializeField] private UnityEvent<string, string, float> _onCommandMatchedWithPhrase;
        [SerializeField] private UnityEvent<string> _onPhraseNormalized;
        [SerializeField] private UnityEvent<string> _onNoCommandMatch;

        private IVoiceCommandResolver _resolver;

        public event Action<string> CommandMatched;

        private void Awake()
        {
            if (_capture == null)
            {
                _capture = GetComponent<VoiceCommandOneShotCapture>();
            }

            if (_capture == null)
            {
                Debug.LogError($"{nameof(VoiceCommandOneShotTranscriptMapper)} cần {nameof(VoiceCommandOneShotCapture)}.", this);
                enabled = false;
                return;
            }

            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VoiceCommandProfile>();
                _profile.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_loadDatasetFromResourcesOnAwake)
            {
                VoiceCommandDatasetFile dataset = VoiceCommandDatasetFile.LoadFromResources(_resourcesDatasetPath);
                _profile.ImportFromDatasetFile(dataset);
            }

            _resolver = new FuzzyVoiceCommandResolver(_profile);
        }

        private void OnEnable()
        {
            if (_capture != null)
            {
                _capture.TranscriptCommitted += OnTranscriptCommitted;
            }
        }

        private void OnDisable()
        {
            if (_capture != null)
            {
                _capture.TranscriptCommitted -= OnTranscriptCommitted;
            }
        }

        /// <summary>
        /// Mục tiêu: Ánh xạ transcript final sang CommandId từ dataset lệnh.
        /// Cách hoạt động: Chuẩn hóa câu nói, fuzzy-match qua resolver, phát UnityEvent cho command/score/phrase.
        /// </summary>
        private void OnTranscriptCommitted(string transcript)
        {
            if (_resolver == null || string.IsNullOrWhiteSpace(transcript))
            {
                return;
            }

            string normalized = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(transcript);
            if (normalized.Length == 0)
            {
                return;
            }

            _onPhraseNormalized?.Invoke(normalized);

            if (!_resolver.TryMapToCanonicalPhrase(
                    normalized,
                    out string commandId,
                    out string canonicalPhrase,
                    out float similarity,
                    out bool isCommandMatch)
                || !isCommandMatch)
            {
                _onNoCommandMatch?.Invoke(normalized);
                GameEventLog.Post($"[VoiceCmd] Không khớp lệnh: {normalized}", GameEventLogCategory.Warning);
                return;
            }

            _onCommandMatched?.Invoke(commandId);
            _onCommandMatchedWithScore?.Invoke(commandId, similarity);
            _onCommandMatchedWithPhrase?.Invoke(commandId, canonicalPhrase, similarity);
            GameEventLog.Post($"[VoiceCmd] {commandId} ({similarity:0.00})", GameEventLogCategory.Info);
            CommandMatched?.Invoke(commandId);
        }
    }
}
