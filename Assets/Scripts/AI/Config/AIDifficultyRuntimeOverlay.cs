namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Snapshot read-only từ <see cref="AIDifficultySO"/> cho một tick — truyền vào config resolver.
    /// </summary>
    public readonly struct AIDifficultyRuntimeOverlay
    {
        public bool HasProfile { get; }
        public float TickInterval { get; }
        public float AttackPowerThreshold { get; }
        public float DefenseRadius { get; }
        public int MinArmyBeforeAttack { get; }
        public bool EnableAttack { get; }
        public int TargetWorkerCountMax { get; }
        public bool RequireBackboneInfra { get; }
        public float WorkerArmyRatioBias { get; }
        public bool RequireVisibleTargets { get; }
        public bool UseUnitCounter { get; }
        public float ScoutIntervalSeconds { get; }

        public AIDifficultyRuntimeOverlay(AIDifficultySO profile)
        {
            if (profile == null)
            {
                HasProfile = false;
                TickInterval = 0f;
                AttackPowerThreshold = 0f;
                DefenseRadius = 0f;
                MinArmyBeforeAttack = 0;
                EnableAttack = true;
                TargetWorkerCountMax = 0;
                RequireBackboneInfra = false;
                WorkerArmyRatioBias = 0f;
                RequireVisibleTargets = true;
                UseUnitCounter = false;
                ScoutIntervalSeconds = 0f;
                return;
            }

            HasProfile = true;
            TickInterval = profile.TickInterval;
            AttackPowerThreshold = profile.AttackPowerThreshold;
            DefenseRadius = profile.DefenseRadius;
            MinArmyBeforeAttack = profile.MinArmyBeforeAttack;
            EnableAttack = profile.EnableAttack;
            TargetWorkerCountMax = profile.TargetWorkerCountMax;
            RequireBackboneInfra = profile.BuildOrderStrictness >= 0.65f;
            WorkerArmyRatioBias = (profile.ExpandAggression - 0.5f) * 0.25f;
            RequireVisibleTargets = profile.UseFairFogMemory;
            UseUnitCounter = profile.UseUnitCounter;
            ScoutIntervalSeconds = profile.ScoutInterval;
        }

        public static AIDifficultyRuntimeOverlay None => new(null);
    }
}
