using System;
using System.Collections.Generic;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// So khớp chuỗi STT với primary/alias; câu tương tự được ánh xạ về PrimaryPhrase mẫu (SRP: fuzzy map).
    /// </summary>
    public sealed class FuzzyVoiceCommandResolver : IVoiceCommandResolver
    {
        private readonly VoiceCommandProfile _profile;
        private readonly List<(string CommandId, string PhraseNormalized)> _candidates = new List<(string, string)>();

        public FuzzyVoiceCommandResolver(VoiceCommandProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            RebuildCandidates();
        }

        public void RebuildCandidates()
        {
            _candidates.Clear();
            foreach (var e in _profile.Commands)
            {
                if (string.IsNullOrWhiteSpace(e.CommandId))
                {
                    continue;
                }

                var id = e.CommandId.Trim();
                AddCandidate(id, e.PrimaryPhrase);
                if (e.Aliases == null)
                {
                    continue;
                }

                foreach (var a in e.Aliases)
                {
                    AddCandidate(id, a);
                }
            }
        }

        public bool TryResolve(
            string recognizedText,
            out string commandId,
            out string canonicalPhraseInDatasetForm,
            out float similarity01)
        {
            if (!TryMapToCanonicalPhrase(
                    recognizedText,
                    out commandId,
                    out canonicalPhraseInDatasetForm,
                    out similarity01,
                    out var isCommandMatch)
                || !isCommandMatch)
            {
                commandId = null;
                canonicalPhraseInDatasetForm = null;
                similarity01 = 0f;
                return false;
            }

            return true;
        }

        public bool TryMapToCanonicalPhrase(
            string recognizedText,
            out string commandId,
            out string canonicalPhraseInDatasetForm,
            out float similarity01,
            out bool isCommandMatch)
        {
            return RecognizedSpeechPhraseMapper.TryMapToCanonicalPhrase(
                _profile,
                _candidates,
                recognizedText,
                out commandId,
                out canonicalPhraseInDatasetForm,
                out similarity01,
                out isCommandMatch);
        }

        private void AddCandidate(string commandId, string phrase)
        {
            var n = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(phrase);
            if (n.Length == 0)
            {
                return;
            }

            _candidates.Add((commandId, n));
        }
    }
}
