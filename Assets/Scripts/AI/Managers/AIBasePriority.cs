namespace GameDevTV.RTS.AI
{
    /// <summary>Priority bands — infra build luôn trên economy gather (400).</summary>
    public static class AIBasePriority
    {
        public const int ReleaseWorkerForInfraBuild = 905;
        public const int BuildStoreHouse = 900;
        public const int BuildCorral = 895;
        public const int BuildForge = 890;
        public const int BuildBarrack = 880;
        public const int BuildDefenseTower = 875;
        public const int TrainWorker = 710;
        /// <summary>Vòng research Forge — trên train/gather, dưới Barrack/Tower.</summary>
        public const int ResearchUpgradeRound = 885;
        /// <summary>Research lẻ sau vòng (chưa dùng — xem xét sau).</summary>
        public const int ResearchUpgrade = 210;
    }
}
