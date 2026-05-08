using System;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Abstraction for streaming speech-to-text. High-level game code depends on this instead of Vosk types (DIP/ISP).
    /// </summary>
    public interface ISpeechRecognitionBackend
    {
        void Initialize();
        void AppendPcm16(short[] buffer, int sampleCount);
        void Flush();
        event Action<string> PartialTextUpdated;
        event Action<string> FinalTextCommitted;
    }
}
