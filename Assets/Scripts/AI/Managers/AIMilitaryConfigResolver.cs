using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Resolve tham số quân sự (tỷ lệ dân/quân, ngưỡng tấn công, lệnh train) cho tick hiện tại.
    /// </summary>
    public static class AIMilitaryConfigResolver
    {
        private const int MaxBuildingQueueSize = 5;
        private const float MinWorkerArmyRatio = 0.15f;
        private const float MaxWorkerArmyRatio = 0.85f;

        /// <summary>
        /// Mục tiêu: Bộ cấu hình military cho planner tick.
        /// Cách hoạt động: Gộp AIMilitarySettings + AIBaseConfigResolver (tower placement, train SO).
        /// </summary>
        public static AIMilitaryRuntimeConfig Resolve(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings militarySettings,
            AIBaseSettings baseSettings,
            AIDifficultyRuntimeOverlay difficulty = default)
        {
            militarySettings ??= AIMilitarySettings.Default;
            AIBaseRuntimeConfig baseConfig = AIBaseConfigResolver.Resolve(snapshot, baseSettings, difficulty);

            float workerArmyRatio = Mathf.Clamp(
                militarySettings.WorkerArmyRatio + (difficulty.HasProfile ? difficulty.WorkerArmyRatioBias : 0f),
                MinWorkerArmyRatio,
                MaxWorkerArmyRatio);
            int targetArmy = ComputeTargetArmyCount(snapshot.Workers.Count, workerArmyRatio);
            Owner enemyOwner = militarySettings.EnemyOwner;

            float attackThreshold = militarySettings.AttackPowerThreshold;
            float defenseRadius = militarySettings.DefenseRadius;
            int minArmy = militarySettings.MinArmyBeforeAttack;
            bool requireVisible = militarySettings.RequireVisibleTargets;
            bool enableAttack = true;

            if (difficulty.HasProfile)
            {
                attackThreshold = difficulty.AttackPowerThreshold;
                defenseRadius = difficulty.DefenseRadius;
                minArmy = difficulty.MinArmyBeforeAttack;
                requireVisible = difficulty.RequireVisibleTargets;
                enableAttack = difficulty.EnableAttack;
            }

            return new AIMilitaryRuntimeConfig(
                enemyOwner,
                workerArmyRatio,
                targetArmy,
                minArmy,
                attackThreshold,
                defenseRadius,
                requireVisible,
                enableAttack,
                militarySettings.MaxTowersInLine,
                militarySettings.TowerLineMinSpacing,
                militarySettings.TowerLineRadius,
                baseConfig.DefenseTowerBuildCommand,
                baseConfig.PlacementSearchRings,
                baseConfig.PlacementSearchStep,
                baseConfig.MaxPlacementFootprint);
        }

        /// <summary>
        /// Mục tiêu: Số lính mục tiêu từ tỷ lệ worker/(worker+army).
        /// Cách hoạt động: army = workers * (1−r) / r.
        /// </summary>
        public static int ComputeTargetArmyCount(int workerCount, float workerArmyRatio)
        {
            float r = Mathf.Clamp(workerArmyRatio, MinWorkerArmyRatio, MaxWorkerArmyRatio);
            if (workerCount <= 0)
            {
                return Mathf.Max(1, Mathf.RoundToInt(4f * (1f - r) / r));
            }

            return Mathf.Max(1, Mathf.RoundToInt(workerCount * (1f - r) / r));
        }

        /// <summary>
        /// Mục tiêu: Thu thập Barrack hoàn thành + lệnh train quân (không Worker).
        /// Cách hoạt động: Inspector AIMilitarySettings → Barrack AvailableCommands → catalog project.
        /// </summary>
        public static void CollectOperationalBarracks(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings,
            List<BaseBuilding> barracksOut,
            List<BuildUnitCommand> trainCommandsOut)
        {
            barracksOut.Clear();
            trainCommandsOut.Clear();

            AIMilitaryUnitMixPlanner.AppendTrainCommandsFromMix(settings, trainCommandsOut);

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (!IsOperationalBarrack(building))
                {
                    continue;
                }

                barracksOut.Add(building);
                CollectMilitaryTrainCommands(building, trainCommandsOut);
            }

            if (trainCommandsOut.Count == 0)
            {
                AIBuildUnitCommandCatalog.AppendMilitaryTrainCommands(trainCommandsOut);
            }
        }

        public static bool IsOperationalBarrack(BaseBuilding building) =>
            building != null
            && building.BuildingSO != null
            && building.BuildingSO.Name == AIInfraBuildUtility.BarrackDisplayName
            && building.Progress.State == BuildingProgress.BuildingState.Completed
            && building.CurrentHealth > 0;

        private static void CollectMilitaryTrainCommands(BaseBuilding barrack, List<BuildUnitCommand> output)
        {
            if (barrack?.AvailableCommands == null)
            {
                return;
            }

            for (int i = 0; i < barrack.AvailableCommands.Length; i++)
            {
                if (barrack.AvailableCommands[i] is not BuildUnitCommand buildUnit
                    || !IsMilitaryTrainCommand(buildUnit))
                {
                    continue;
                }

                if (!output.Contains(buildUnit))
                {
                    output.Add(buildUnit);
                }
            }
        }

        private static bool IsMilitaryTrainCommand(BuildUnitCommand command)
        {
            if (command?.Unit == null)
            {
                return false;
            }

            string name = command.Unit.Name;
            return !string.IsNullOrEmpty(name)
                && !name.Contains("Worker", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Mục tiêu: Chọn lệnh train theo tỷ lệ 3 slot; fallback xoay vòng nếu chưa gán mix.
        /// Cách hoạt động: <see cref="AIMilitaryUnitMixPlanner"/>; không có slot → duyệt available.
        /// </summary>
        public static BuildUnitCommand PickTrainCommand(
            AIWorldStateSnapshot snapshot,
            BaseBuilding barrack,
            IReadOnlyList<BuildUnitCommand> available,
            AIMilitarySettings settings)
        {
            if (barrack == null)
            {
                return null;
            }

            int s0 = 0;
            int s1 = 0;
            int s2 = 0;
            BuildUnitCommand fromMix = AIMilitaryUnitMixPlanner.PickTrainCommand(
                snapshot,
                barrack,
                settings,
                ref s0,
                ref s1,
                ref s2);
            if (fromMix != null)
            {
                return fromMix;
            }

            if (available == null || available.Count == 0)
            {
                return null;
            }

            int start = barrack.QueueSize % available.Count;
            for (int offset = 0; offset < available.Count; offset++)
            {
                BuildUnitCommand candidate = available[(start + offset) % available.Count];
                if (CanEnqueueTrain(snapshot, barrack, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static bool CanEnqueueTrain(
            AIWorldStateSnapshot snapshot,
            BaseBuilding barrack,
            BuildUnitCommand trainCommand)
        {
            if (barrack == null || trainCommand == null || barrack.QueueSize >= MaxBuildingQueueSize)
            {
                return false;
            }

            CommandContext context = new(snapshot.Owner, barrack, AIHitUtility.AtPoint(barrack.transform.position));
            return !trainCommand.IsLocked(context) && trainCommand.IsAvailable(context);
        }

    }

    /// <summary>Tham số military đã resolve — read-only cho một tick.</summary>
    public readonly struct AIMilitaryRuntimeConfig
    {
        public Owner EnemyOwner { get; }
        public float WorkerArmyRatio { get; }
        public int TargetArmyCount { get; }
        public int MinArmyBeforeAttack { get; }
        public float AttackPowerThreshold { get; }
        public float DefenseRadius { get; }
        public bool RequireVisibleTargets { get; }
        public bool EnableAttack { get; }
        public int MaxTowersInLine { get; }
        public float TowerLineMinSpacing { get; }
        public float TowerLineRadius { get; }
        public BuildBuildingCommand DefenseTowerBuildCommand { get; }
        public int PlacementSearchRings { get; }
        public float PlacementSearchStep { get; }
        public float MaxPlacementFootprint { get; }

        public AIMilitaryRuntimeConfig(
            Owner enemyOwner,
            float workerArmyRatio,
            int targetArmyCount,
            int minArmyBeforeAttack,
            float attackPowerThreshold,
            float defenseRadius,
            bool requireVisibleTargets,
            bool enableAttack,
            int maxTowersInLine,
            float towerLineMinSpacing,
            float towerLineRadius,
            BuildBuildingCommand defenseTowerBuildCommand,
            int placementSearchRings,
            float placementSearchStep,
            float maxPlacementFootprint)
        {
            EnemyOwner = enemyOwner;
            WorkerArmyRatio = workerArmyRatio;
            TargetArmyCount = targetArmyCount;
            MinArmyBeforeAttack = minArmyBeforeAttack;
            AttackPowerThreshold = attackPowerThreshold;
            DefenseRadius = defenseRadius;
            RequireVisibleTargets = requireVisibleTargets;
            EnableAttack = enableAttack;
            MaxTowersInLine = maxTowersInLine;
            TowerLineMinSpacing = towerLineMinSpacing;
            TowerLineRadius = towerLineRadius;
            DefenseTowerBuildCommand = defenseTowerBuildCommand;
            PlacementSearchRings = placementSearchRings;
            PlacementSearchStep = placementSearchStep;
            MaxPlacementFootprint = maxPlacementFootprint;
        }
    }

    /// <summary>
    /// Tham số quân sự trên <see cref="AIController"/> — Inspector; số liệu mặc định theo plan difficulty preset.
    /// </summary>
    [System.Serializable]
    public sealed class AIMilitarySettings
    {
        [Header("Địch")]
        [SerializeField] private Owner enemyOwner = Owner.Player1;

        [Header("Tỷ lệ dân / quân")]
        [Tooltip("Tỷ lệ worker trên tổng lực lượng (0.5 = nửa dân nửa quân).")]
        [SerializeField] [Range(0.15f, 0.85f)] private float workerArmyRatio = 0.5f;

        [Header("Tấn công")]
        [SerializeField] private int minArmyBeforeAttack = 6;
        [Tooltip("Khi scout thấy CC địch / lính địch (không animal) — rally ngay nếu có ít nhất N quân.")]
        [SerializeField] private int minArmyOnHostileContact = 1;
        [Tooltip("Khi map chưa khám phá hết — chỉ gọi tỷ lệ này đi đánh; phần còn lại tiếp tục patrol.")]
        [SerializeField] [Range(0.5f, 1f)] private float rallyFractionWhileExploring = 0.8f;
        [Tooltip("Tập hợp quanh nhà mình — bán kính từ Civil Central về hướng địch (không tại chỗ thấy CC).")]
        [SerializeField] private float rallyRegroupStagingRadiusFromCc = 48f;
        [Tooltip("Tối đa bao nhiêu % quãng đường tới địch — tránh điểm tụ quá gần contact.")]
        [SerializeField] [Range(0.1f, 0.5f)] private float rallyRegroupMaxThreatDistanceFraction = 0.28f;
        [SerializeField] private float rallyRegroupGatherRadius = 18f;
        [SerializeField] [Range(0.3f, 1f)] private float rallyRegroupRequiredFraction = 0.45f;
        [Tooltip("AI tấn công khi army >= threshold * quân địch ước lượng gần CC địch.")]
        [SerializeField] private float attackPowerThreshold = 1.2f;

        [Header("Phòng thủ")]
        [SerializeField] private float defenseRadius = 42f;
        [SerializeField] private bool requireVisibleTargets = true;

        [Header("Tháp (hàng phòng thủ sau tháp đầu từ Base)")]
        [SerializeField] private int maxTowersInLine = 3;
        [SerializeField] private float towerLineRadius = 28f;
        [SerializeField] private float towerLineMinSpacing = 14f;

        [Header("Cơ cấu quân — 3 loại (% trên tổng quân)")]
        [Tooltip("Spawn Weight = tỷ lệ (50/30/20). AI train loại đang thiếu % so với tổng quân + queue.")]
        [SerializeField] private AIMilitaryUnitMixSlot unitMixSlot1 = new();
        [SerializeField] private AIMilitaryUnitMixSlot unitMixSlot2 = new();
        [SerializeField] private AIMilitaryUnitMixSlot unitMixSlot3 = new();

        [Header("Tuần tra — quân rảnh mở map")]
        [SerializeField] private bool enableIdlePatrol = true;
        [SerializeField] private float patrolMinRadius = 22f;
        [Tooltip("0 = không giới hạn bán kính — vòng patrol mở rộng dần theo thời gian.")]
        [SerializeField] private float patrolMaxRadius;
        [SerializeField] private float patrolRingStep = 16f;
        [Tooltip("Tối đa lính nhận lệnh patrol mỗi tick AI (tránh spam queue).")]
        [SerializeField] private int maxPatrolAssignmentsPerTick = 16;
        [Tooltip("Tỷ lệ ô explored trong vòng patrol (fog) coi là đã khám phá hết — gọi 100% quân đi đánh.")]
        [SerializeField] [Range(0.75f, 1f)] private float mapExplorationCompleteCoverage = 0.92f;
        [Header("Gộp nhóm (formation move)")]
        [SerializeField] private float squadMergeRadius = 14f;
        [Tooltip("Đứng gần nhau bao lâu (giây) trước khi gộp squad.")]
        [SerializeField] private float squadMergeHoldSeconds = 2.5f;
        [SerializeField] private int minSquadMembers = 2;

        public Owner EnemyOwner => enemyOwner;
        public float WorkerArmyRatio => workerArmyRatio;
        public int MinArmyBeforeAttack => minArmyBeforeAttack;
        public int MinArmyOnHostileContact => minArmyOnHostileContact;
        public float RallyFractionWhileExploring => rallyFractionWhileExploring;
        public float RallyRegroupStagingRadiusFromCc => rallyRegroupStagingRadiusFromCc;
        public float RallyRegroupMaxThreatDistanceFraction => rallyRegroupMaxThreatDistanceFraction;
        public float RallyRegroupGatherRadius => rallyRegroupGatherRadius;
        public float RallyRegroupRequiredFraction => rallyRegroupRequiredFraction;
        public float AttackPowerThreshold => attackPowerThreshold;
        public float DefenseRadius => defenseRadius;
        public bool RequireVisibleTargets => requireVisibleTargets;
        public int MaxTowersInLine => maxTowersInLine;
        public float TowerLineRadius => towerLineRadius;
        public float TowerLineMinSpacing => towerLineMinSpacing;
        public AIMilitaryUnitMixSlot UnitMixSlot1 => unitMixSlot1;
        public AIMilitaryUnitMixSlot UnitMixSlot2 => unitMixSlot2;
        public AIMilitaryUnitMixSlot UnitMixSlot3 => unitMixSlot3;
        public bool EnableIdlePatrol => enableIdlePatrol;
        public float PatrolMinRadius => patrolMinRadius;
        public float PatrolMaxRadius => patrolMaxRadius;
        public float PatrolRingStep => patrolRingStep;
        public int MaxPatrolAssignmentsPerTick => maxPatrolAssignmentsPerTick;
        public float MapExplorationCompleteCoverage => mapExplorationCompleteCoverage;
        public float SquadMergeRadius => squadMergeRadius;
        public float SquadMergeHoldSeconds => squadMergeHoldSeconds;
        public int MinSquadMembers => minSquadMembers;

        public static AIMilitarySettings Default => new();
    }
}
