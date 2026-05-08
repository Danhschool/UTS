using System;
using System.Collections.Generic;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// So khớp chuỗi STT với primary/alias bằng độ tương đồng Levenshtein chuẩn hóa (SRP: fuzzy map).
    /// </summary>
    public sealed class FuzzyVoiceCommandResolver : IVoiceCommandResolver
    {
        private readonly VoiceCommandProfile _profile;
        private readonly List<(string CommandId, string Phrase)> _candidates = new List<(string, string)>();

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

        /// <summary>
        /// Mục tiêu: chọn CommandId có cụm gần nhất. Logic: NormalizedSimilarity với mọi cụm, lấy max nếu ≥ MinSimilarity.
        /// </summary>
        public bool TryResolve(string recognizedText, out string commandId, out float similarity01)
        {
            commandId = null;
            similarity01 = 0f;
            if (string.IsNullOrWhiteSpace(recognizedText) || _candidates.Count == 0)
            {
                return false;
            }

            var input = VoiceCommandProfile.NormalizeForMatch(recognizedText);
            if (input.Length == 0)
            {
                return false;
            }

            var bestSim = 0f;
            string bestId = null;
            foreach (var (cid, phrase) in _candidates)
            {
                var sim = StringSimilarity.NormalizedSimilarity(input, phrase);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestId = cid;
                }
            }

            if (bestId != null && bestSim >= _profile.MinSimilarity)
            {
                commandId = bestId;
                similarity01 = bestSim;
                return true;
            }

            return false;
        }

        private void AddCandidate(string commandId, string phrase)
        {
            var n = VoiceCommandProfile.NormalizeForMatch(phrase);
            if (n.Length == 0)
            {
                return;
            }

            _candidates.Add((commandId, n));
        }
    }
}
