namespace GameDevTV.RTS.UI.GameEventLog
{
    public readonly struct GameEventLogLine
    {
        public string Message { get; }
        public GameEventLogCategory Category { get; }
        public float TimeSeconds { get; }

        public GameEventLogLine(string message, GameEventLogCategory category, float timeSeconds)
        {
            Message = message;
            Category = category;
            TimeSeconds = timeSeconds;
        }
    }
}
