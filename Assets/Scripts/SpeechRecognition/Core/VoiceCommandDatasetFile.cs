using System;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Root JSON cho tập lệnh thoại (OCP: mở rộng bằng file, không sửa code gameplay).
    /// JsonUtility: tên field khớp với JSON (schemaVersion, minSimilarity, commands).
    /// </summary>
    [Serializable]
    public sealed class VoiceCommandDatasetFile
    {
        public int schemaVersion = 1;

        public float minSimilarity = 0.72f;

        public VoiceCommandEntry[] commands = Array.Empty<VoiceCommandEntry>();

        /// <summary>
        /// Mục tiêu: Parse file JSON thành object dùng cho VoiceCommandProfile hoặc test.
        /// Cách hoạt động: JsonUtility.FromJson — cần khóa "commands" và từng entry có CommandId/PrimaryPhrase/Aliases.
        /// </summary>
        public static VoiceCommandDatasetFile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new VoiceCommandDatasetFile();
            }

            return JsonUtility.FromJson<VoiceCommandDatasetFile>(json);
        }

        /// <summary>
        /// Mục tiêu: Đọc TextAsset JSON trong thư mục Resources (không cần đuôi .json).
        /// Cách hoạt động: Resources.Load rồi FromJson; log lỗi nếu thiếu file.
        /// </summary>
        public static VoiceCommandDatasetFile LoadFromResources(string resourcePathWithoutExtension = "VoiceCommands/rts_voice_commands_standard_vi")
        {
            var asset = Resources.Load<TextAsset>(resourcePathWithoutExtension);
            if (asset == null)
            {
                Debug.LogError(
                    $"Không tìm thấy TextAsset Resources/{resourcePathWithoutExtension}.json — đặt file JSON vào Assets/Resources/...",
                    null);
                return new VoiceCommandDatasetFile();
            }

            return FromJson(asset.text);
        }
    }
}
