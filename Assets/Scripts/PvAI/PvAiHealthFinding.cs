using System;

namespace GameDevTV.RTS.PvAI
{
    public enum PvAiHealthSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Một kết quả kiểm tra PvAI (pass hoặc fail kèm mô tả).
    /// </summary>
    [Serializable]
    public readonly struct PvAiHealthFinding
    {
        public string CheckId { get; }
        public string HypothesisId { get; }
        public PvAiHealthSeverity Severity { get; }
        public string Message { get; }
        public bool Passed => Severity == PvAiHealthSeverity.Info;

        public PvAiHealthFinding(string checkId, string hypothesisId, PvAiHealthSeverity severity, string message)
        {
            CheckId = checkId;
            HypothesisId = hypothesisId;
            Severity = severity;
            Message = message;
        }

        public static PvAiHealthFinding Pass(string checkId, string message) =>
            new(checkId, string.Empty, PvAiHealthSeverity.Info, message);

        public static PvAiHealthFinding Fail(string checkId, string hypothesisId, PvAiHealthSeverity severity, string message) =>
            new(checkId, hypothesisId, severity, message);
    }
}
