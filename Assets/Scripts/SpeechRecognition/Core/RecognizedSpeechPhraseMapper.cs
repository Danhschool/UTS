using System.Collections.Generic;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Tìm cụm mẫu gần nhất và trả PrimaryPhrase chuẩn (SRP: ánh xạ câu tương tự → mẫu dataset).
    /// </summary>
    public static class RecognizedSpeechPhraseMapper
    {
        /// <summary>
        /// Mục tiêu: Chọn CommandId có cụm gần nhất (primary hoặc alias) và trả PrimaryPhrase đã chuẩn hóa.
        /// Cách hoạt động: So similarity với mọi cụm; isCommandMatch khi ≥ MinSimilarity, snap khi ≥ PhraseSnapMinSimilarity.
        /// </summary>
        public static bool TryMapToCanonicalPhrase(
            VoiceCommandProfile profile,
            IReadOnlyList<(string CommandId, string PhraseNormalized)> candidates,
            string recognizedText,
            out string commandId,
            out string canonicalPhraseInDatasetForm,
            out float similarity01,
            out bool isCommandMatch)
        {
            commandId = null;
            canonicalPhraseInDatasetForm = null;
            similarity01 = 0f;
            isCommandMatch = false;

            if (profile == null || candidates == null || candidates.Count == 0)
            {
                return false;
            }

            var input = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(recognizedText);
            if (input.Length == 0)
            {
                return false;
            }

            var bestSim = 0f;
            string bestId = null;
            foreach (var (cid, phrase) in candidates)
            {
                var sim = StringSimilarity.NormalizedSimilarity(input, phrase);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestId = cid;
                }
            }

            if (bestId == null || bestSim < profile.PhraseSnapMinSimilarity)
            {
                return false;
            }

            if (!profile.TryGetCanonicalPhraseDatasetForm(bestId, out var canonical))
            {
                return false;
            }

            commandId = bestId;
            canonicalPhraseInDatasetForm = canonical;
            similarity01 = bestSim;
            isCommandMatch = bestSim >= profile.MinSimilarity;
            return true;
        }
    }
}
