namespace GameDevTV.RTS.AI
{
    /// <summary>Priority bands — gather thấp hơn base infra build (760+).</summary>
    public static class AIEconomyPriority
    {
        public const int ResumeWorkerAfterBuild = 860;
        public const int ReturnSupplies = 550;
        public const int GatherVisibleSupply = 400;
        public const int BuildRemoteStore = 520;
    }
}
