using Whisper;
using Whisper.Utils;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Gợi ý thông số streaming Whisper (SRP: chỉ tuning, không chạy inference).
    /// </summary>
    public static class WhisperStreamingTuning
    {
        /// <summary>Prefab / mặc định cân bằng (step 1.5s).</summary>
        public const float BalancedStepSec = 1.5f;

        /// <summary>Hướng độ trễ ~1–3s: ít audio mỗi lần infer + bước ngắn hơn.</summary>
        public const float LowLatencyStepSec = 1f;

        public const float LowLatencyKeepSec = 0.1f;
        public const float LowLatencyLengthSec = 5f;
        public const float LowLatencyMicChunkSec = 0.3f;
        public const float LowLatencyVadLastSec = 0.7f;

        /// <summary>
        /// Mục tiêu: Giảm thời gian chờ và khối audio mỗi lần Whisper chạy.
        /// Cách hoạt động: Hạ stepSec/lengthSec, bật singleSegment + dropOldBuffer, thu micro chunk nhỏ hơn.
        /// </summary>
        public static void ApplyLowLatency(WhisperManager whisper, MicrophoneRecord microphone)
        {
            if (whisper != null)
            {
                whisper.stepSec = LowLatencyStepSec;
                whisper.keepSec = LowLatencyKeepSec;
                whisper.lengthSec = LowLatencyLengthSec;
                whisper.dropOldBuffer = true;
                whisper.updatePrompt = false;
                whisper.singleSegment = true;
                whisper.useVad = true;
                whisper.noContext = true;
            }

            if (microphone != null)
            {
                microphone.chunksLengthSec = LowLatencyMicChunkSec;
                microphone.useVad = true;
                microphone.vadLastSec = LowLatencyVadLastSec;
                microphone.dropVadPart = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Khôi phục cấu hình ổn định hơn (ít giật transcript).
        /// Cách hoạt động: step/length lớn hơn, tắt singleSegment ép một đoạn.
        /// </summary>
        public static void ApplyBalanced(WhisperManager whisper, MicrophoneRecord microphone)
        {
            if (whisper != null)
            {
                whisper.stepSec = BalancedStepSec;
                whisper.keepSec = 0.2f;
                whisper.lengthSec = 10f;
                whisper.dropOldBuffer = false;
                whisper.updatePrompt = true;
                whisper.singleSegment = false;
                whisper.useVad = true;
            }

            if (microphone != null)
            {
                microphone.chunksLengthSec = 0.5f;
                microphone.vadLastSec = 1.25f;
            }
        }
    }
}
