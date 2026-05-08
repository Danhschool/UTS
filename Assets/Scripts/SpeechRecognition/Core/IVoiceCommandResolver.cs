namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Ánh xạ chuỗi STT sang id lệnh game (DIP: gameplay không phụ thuộc Vosk).
    /// </summary>
    public interface IVoiceCommandResolver
    {
        bool TryResolve(string recognizedText, out string commandId, out float similarity01);
    }
}
