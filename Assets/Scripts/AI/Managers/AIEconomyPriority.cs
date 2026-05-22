namespace GameDevTV.RTS.AI
{
    /// <summary>Priority bands — gather thấp hơn base infra build (760+).</summary>
    public static class AIEconomyPriority
    {
        public const int ResumeWorkerAfterBuild = 860;
        /// <summary>Stop trước — ngắt gather BT / khóa mỏ cũ (tick refresh mỗi N tick).</summary>
        public const int WorkerGatherRefreshStop = 845;
        /// <summary>Move ngắn sau Stop — cùng tick, priority thấp hơn Stop một chút.</summary>
        public const int WorkerGatherRefreshMove = 844;
        public const int ReturnSupplies = 550;
        public const int HuntWildAnimalForFood = 410;
        public const int GatherVisibleSupply = 400;
        public const int BuildRemoteStore = 520;

        public static bool IsGatherRefreshPriority(int priority) =>
            priority == WorkerGatherRefreshStop || priority == WorkerGatherRefreshMove;
    }
}
