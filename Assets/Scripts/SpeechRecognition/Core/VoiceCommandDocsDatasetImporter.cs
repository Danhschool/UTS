using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Chuyển file docs/voice-command-dataset.vi.json (schema intents) sang VoiceCommandDatasetFile (commands).
    /// </summary>
    public static class VoiceCommandDocsDatasetImporter
    {
        [Serializable]
        private sealed class DocsIntentEntry
        {
            public string intent_id;
            public string canonical_command;
            public string[] examples_vi;
        }

        [Serializable]
        private sealed class DocsRoot
        {
            public DocsIntentEntry[] intents;
        }

        /// <summary>
        /// Mục tiêu: Dùng dataset tài liệu (intents + examples_vi) cho JsonUtility runtime.
        /// Cách hoạt động: Parse DocsRoot, map intent_id → CommandId, câu đầu → PrimaryPhrase, phần còn → Aliases.
        /// </summary>
        public static VoiceCommandDatasetFile FromDocsJson(string json, float minSimilarity = 0.72f)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new VoiceCommandDatasetFile();
            }

            var docs = JsonUtility.FromJson<DocsRoot>(json);
            var commands = new List<VoiceCommandEntry>();
            if (docs?.intents == null)
            {
                return new VoiceCommandDatasetFile { minSimilarity = minSimilarity, commands = Array.Empty<VoiceCommandEntry>() };
            }

            foreach (var intent in docs.intents)
            {
                if (intent == null || string.IsNullOrWhiteSpace(intent.intent_id))
                {
                    continue;
                }

                var phrases = CollectPhrases(intent);
                if (phrases.Count == 0)
                {
                    continue;
                }

                var aliases = new List<string>();
                for (var i = 1; i < phrases.Count; i++)
                {
                    aliases.Add(phrases[i]);
                }

                commands.Add(new VoiceCommandEntry
                {
                    CommandId = intent.intent_id,
                    PrimaryPhrase = phrases[0],
                    Aliases = aliases.ToArray()
                });
            }

            return new VoiceCommandDatasetFile
            {
                schemaVersion = 1,
                minSimilarity = minSimilarity,
                commands = commands.ToArray()
            };
        }

        /// <summary>
        /// Mục tiêu: Serialize dataset runtime ra JSON Resources (schema commands).
        /// Cách hoạt động: JsonUtility.ToJson trên VoiceCommandDatasetFile.
        /// </summary>
        public static string ToRuntimeJson(VoiceCommandDatasetFile dataset, bool prettyPrint = true)
        {
            if (dataset == null)
            {
                return "{}";
            }

            return JsonUtility.ToJson(dataset, prettyPrint);
        }

        private static List<string> CollectPhrases(DocsIntentEntry intent)
        {
            var phrases = new List<string>();
            if (intent.examples_vi != null)
            {
                foreach (var raw in intent.examples_vi)
                {
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        phrases.Add(raw.Trim());
                    }
                }
            }

            if (phrases.Count == 0 && !string.IsNullOrWhiteSpace(intent.canonical_command))
            {
                phrases.Add(intent.canonical_command.Replace('_', ' '));
            }

            return phrases;
        }
    }
}
