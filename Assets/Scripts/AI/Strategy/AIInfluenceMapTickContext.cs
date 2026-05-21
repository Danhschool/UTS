namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Snapshot influence map đã rebuild cho tick hiện tại — managers đọc, không rebuild lại.
    /// </summary>
    public readonly struct AIInfluenceMapTickContext
    {
        public AIInfluenceMap Map { get; }
        public AIInfluenceMapRuntimeConfig Config { get; }
        public bool IsValid { get; }

        public AIInfluenceMapTickContext(
            AIInfluenceMap map,
            AIInfluenceMapRuntimeConfig config,
            bool isValid)
        {
            Map = map;
            Config = config;
            IsValid = isValid;
        }

        public static AIInfluenceMapTickContext Empty => default;
    }
}
