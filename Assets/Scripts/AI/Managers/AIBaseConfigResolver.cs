using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Suy ra lệnh train/build/research và ngưỡng worker từ Civil Central + worker commands.
    /// </summary>
    public static class AIBaseConfigResolver
    {
        /// <summary>
        /// Mục tiêu: Bộ cấu hình base cho tick hiện tại.
        /// Cách hoạt động: Quét CC/workers/buildings; tính target worker và surplus research.
        /// </summary>
        public static AIBaseRuntimeConfig Resolve(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings manualOverrides,
            AIDifficultyRuntimeOverlay difficulty = default)
        {
            manualOverrides ??= AIBaseSettings.Default;

            BuildUnitCommand trainWorker = ResolveTrainWorkerCommand(snapshot, manualOverrides);
            BuildBuildingCommand corralBuild = ResolveBuildCommand(snapshot, manualOverrides, AIInfraBuildUtility.CorralDisplayName);
            BuildBuildingCommand forgeBuild = ResolveBuildCommand(snapshot, manualOverrides, AIInfraBuildUtility.ForgeDisplayName);
            BuildBuildingCommand storeBuild = ResolveBuildCommand(snapshot, manualOverrides, AIInfraBuildUtility.StoreHouseDisplayName);
            BuildBuildingCommand barrackBuild = ResolveBuildCommand(snapshot, manualOverrides, AIInfraBuildUtility.BarrackDisplayName);
            BuildBuildingCommand towerBuild = ResolveBuildCommand(snapshot, manualOverrides, AIInfraBuildUtility.DefenseTowerDisplayName);
            ResearchUpgradeCommand research = ResolveResearchCommand(snapshot, manualOverrides);

            int targetWorkers = ResolveWorkerCap(snapshot, manualOverrides, difficulty);
            int surplusStone = ComputeSurplusThreshold(snapshot);
            GetWorkerTrainReserves(manualOverrides, out int reserveStone, out int reserveWood, out int reserveFood);
            BuildBuildingCommand placementRef = corralBuild ?? forgeBuild ?? storeBuild ?? barrackBuild ?? towerBuild;
            GetPlacementSearch(placementRef, out int rings, out float step);
            float maxFootprint = ComputeMaxPlacementFootprint(
                corralBuild,
                forgeBuild,
                storeBuild,
                barrackBuild,
                towerBuild);

            bool requireBackbone = manualOverrides.RequireBackboneBeforeBarrackAndTower;
            if (difficulty.HasProfile)
            {
                requireBackbone = difficulty.RequireBackboneInfra;
            }
            float obstacleProbeRadius = manualOverrides.PlacementNavMeshObstacleProbeRadius;
            LayerMask obstacleProbeLayers = manualOverrides.PlacementNavMeshObstacleProbeLayers;

            return new AIBaseRuntimeConfig(
                trainWorker,
                corralBuild,
                forgeBuild,
                storeBuild,
                barrackBuild,
                towerBuild,
                research,
                targetWorkers,
                reserveStone,
                reserveWood,
                reserveFood,
                surplusStone,
                rings,
                step,
                maxFootprint,
                requireBackbone,
                obstacleProbeRadius,
                obstacleProbeLayers);
        }

        private static BuildUnitCommand ResolveTrainWorkerCommand(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings)
        {
            if (settings?.TrainWorkerCommand != null)
            {
                return settings.TrainWorkerCommand;
            }

            BaseBuilding cc = snapshot.CivilCentral;
            if (cc?.AvailableCommands == null)
            {
                return null;
            }

            for (int i = 0; i < cc.AvailableCommands.Length; i++)
            {
                if (cc.AvailableCommands[i] is BuildUnitCommand buildUnit
                    && IsWorkerUnit(buildUnit.Unit))
                {
                    return buildUnit;
                }
            }

            return null;
        }

        /// <summary>
        /// Mục tiêu: Lệnh BuildBuilding cho tên nhà — Inspector ưu tiên, không có thì quét worker.
        /// Cách hoạt động: So khớp BuildingSO.Name với display name chuẩn.
        /// </summary>
        public static BuildBuildingCommand ResolveBuildCommand(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings,
            string buildingDisplayName)
        {
            BuildBuildingCommand assigned = TryGetAssignedBuildCommand(settings, buildingDisplayName);
            if (assigned != null)
            {
                return assigned;
            }

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                if (TryFindBuildCommandOnUnit(snapshot.Workers[i], buildingDisplayName, out BuildBuildingCommand fromWorker))
                {
                    return fromWorker;
                }
            }

            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                if (snapshot.Units[i] is Worker)
                {
                    continue;
                }

                if (TryFindBuildCommandOnUnit(snapshot.Units[i], buildingDisplayName, out BuildBuildingCommand fromUnit))
                {
                    return fromUnit;
                }
            }

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building == null || building.Owner != snapshot.Owner)
                {
                    continue;
                }

                if (TryFindBuildCommandOnBuilding(building, buildingDisplayName, out BuildBuildingCommand fromBuilding))
                {
                    return fromBuilding;
                }
            }

            return AIBuildCommandCatalog.FindByBuildingDisplayName(buildingDisplayName);
        }

        private static bool TryFindBuildCommandOnBuilding(
            BaseBuilding building,
            string buildingDisplayName,
            out BuildBuildingCommand buildCommand)
        {
            buildCommand = null;
            if (building?.AvailableCommands == null)
            {
                return false;
            }

            for (int i = 0; i < building.AvailableCommands.Length; i++)
            {
                if (building.AvailableCommands[i] is BuildBuildingCommand build
                    && build.Building != null
                    && build.Building.Name == buildingDisplayName)
                {
                    buildCommand = build;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindBuildCommandOnUnit(
            AbstractUnit unit,
            string buildingDisplayName,
            out BuildBuildingCommand buildCommand)
        {
            buildCommand = null;
            if (unit == null)
            {
                return false;
            }

            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int c = 0; c < commands.Count; c++)
            {
                if (commands[c] is BuildBuildingCommand build
                    && build.Building != null
                    && build.Building.Name == buildingDisplayName)
                {
                    buildCommand = build;
                    return true;
                }
            }

            return false;
        }

        private static ResearchUpgradeCommand ResolveResearchCommand(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings)
        {
            if (settings?.ResearchUpgradeCommand != null)
            {
                return settings.ResearchUpgradeCommand;
            }

            BaseBuilding forge = FindBuildingByName(snapshot, AIInfraBuildUtility.ForgeDisplayName);
            if (forge == null)
            {
                return null;
            }

            CommandContext context = new(snapshot.Owner, forge, AIHitUtility.AtPoint(forge.transform.position));
            return TryResolveFirstAvailableResearch(forge, context);
        }

        /// <summary>
        /// Mục tiêu: Chọn upgrade research tiếp theo AI có thể enqueue tại Forge.
        /// Cách hoạt động: Duyệt AvailableCommands; trả lệnh ResearchUpgrade đầu tiên không Locked và Available.
        /// </summary>
        public static ResearchUpgradeCommand TryResolveFirstAvailableResearch(
            BaseBuilding forge,
            CommandContext context)
        {
            if (forge?.AvailableCommands == null)
            {
                return null;
            }

            for (int i = 0; i < forge.AvailableCommands.Length; i++)
            {
                if (forge.AvailableCommands[i] is not ResearchUpgradeCommand research)
                {
                    continue;
                }

                if (research.IsLocked(context) || !research.IsAvailable(context))
                {
                    continue;
                }

                return research;
            }

            return null;
        }

        /// <summary>
        /// Mục tiêu: Cap worker theo giai đoạn infra (Store → Corral → Forge) + cap tổng Inspector.
        /// Cách hoạt động: Chưa Store → untilStore; chưa Corral → untilCorral; chưa Forge → untilForge; sau đó maxTotal.
        /// </summary>
        public static int ResolveWorkerCap(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings,
            AIDifficultyRuntimeOverlay difficulty = default)
        {
            settings ??= AIBaseSettings.Default;
            int autoCap = ComputeAutoWorkerCapFromPopulation(snapshot);

            if (difficulty.HasProfile && difficulty.TargetWorkerCountMax > 0)
            {
                autoCap = Mathf.Min(autoCap, difficulty.TargetWorkerCountMax);
            }

            if (!AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.StoreHouseDisplayName))
            {
                return settings.MaxWorkersUntilStoreBuilt > 0
                    ? settings.MaxWorkersUntilStoreBuilt
                    : autoCap;
            }

            if (!AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.CorralDisplayName))
            {
                return settings.MaxWorkersUntilCorralBuilt > 0
                    ? settings.MaxWorkersUntilCorralBuilt
                    : autoCap;
            }

            if (!AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.ForgeDisplayName))
            {
                return settings.MaxWorkersUntilForgeBuilt > 0
                    ? settings.MaxWorkersUntilForgeBuilt
                    : autoCap;
            }

            return settings.MaxWorkersTotal > 0 ? settings.MaxWorkersTotal : autoCap;
        }

        private static int ComputeAutoWorkerCapFromPopulation(AIWorldStateSnapshot snapshot)
        {
            int popLimit = snapshot.PopulationLimit > 0 ? snapshot.PopulationLimit : 20;
            int fromPop = Mathf.Clamp(Mathf.RoundToInt(popLimit * 0.38f), 4, 14);
            return Mathf.Max(fromPop, 4);
        }

        private static void GetWorkerTrainReserves(
            AIBaseSettings settings,
            out int reserveStone,
            out int reserveWood,
            out int reserveFood)
        {
            settings ??= AIBaseSettings.Default;
            reserveStone = Mathf.Max(0, settings.ReserveStoneBeforeTrain);
            reserveWood = Mathf.Max(0, settings.ReserveWoodBeforeTrain);
            reserveFood = Mathf.Max(0, settings.ReserveFoodBeforeTrain);
        }

        private static int ComputeSurplusThreshold(AIWorldStateSnapshot snapshot)
        {
            int workers = Mathf.Max(1, snapshot.Workers.Count);
            return Mathf.Clamp(workers * 25 + 80, 120, 400);
        }

        private static void GetPlacementSearch(
            BuildBuildingCommand buildCommand,
            out int rings,
            out float step)
        {
            float footprint = GetPlacementFootprint(buildCommand);
            step = Mathf.Clamp(footprint * 0.45f, 3f, 8f);
            rings = Mathf.Clamp(Mathf.CeilToInt(footprint / step) + 3, 5, 10);
        }

        private static float GetPlacementFootprint(BuildBuildingCommand buildCommand)
        {
            if (buildCommand?.Restrictions == null || buildCommand.Restrictions.Length == 0)
            {
                return 10f;
            }

            float footprint = 4f;
            BuildingRestrictionSO[] restrictions = buildCommand.Restrictions;
            for (int i = 0; i < restrictions.Length; i++)
            {
                BuildingRestrictionSO restriction = restrictions[i];
                if (restriction == null)
                {
                    continue;
                }

                footprint = Mathf.Max(
                    footprint,
                    restriction.Radius,
                    restriction.Extents.x,
                    restriction.Extents.z);
            }

            return footprint;
        }

        private static float ComputeMaxPlacementFootprint(params BuildBuildingCommand[] commands)
        {
            float footprint = 10f;
            for (int i = 0; i < commands.Length; i++)
            {
                footprint = Mathf.Max(footprint, GetPlacementFootprint(commands[i]));
            }

            return footprint;
        }

        private static BuildBuildingCommand TryGetAssignedBuildCommand(AIBaseSettings settings, string buildingDisplayName)
        {
            if (settings == null)
            {
                return null;
            }

            if (buildingDisplayName == AIInfraBuildUtility.CorralDisplayName)
            {
                return settings.BuildCorralCommand;
            }

            if (buildingDisplayName == AIInfraBuildUtility.ForgeDisplayName)
            {
                return settings.BuildForgeCommand;
            }

            if (buildingDisplayName == AIInfraBuildUtility.StoreHouseDisplayName)
            {
                return settings.BuildStoreHouseCommand;
            }

            if (buildingDisplayName == AIInfraBuildUtility.BarrackDisplayName)
            {
                return settings.BuildBarrackCommand;
            }

            if (buildingDisplayName == AIInfraBuildUtility.DefenseTowerDisplayName)
            {
                return settings.BuildDefenseTowerCommand;
            }

            return null;
        }

        private static bool IsWorkerUnit(AbstractUnitSO unitSo) =>
            unitSo != null
            && (unitSo.Name.Contains("Worker", System.StringComparison.OrdinalIgnoreCase)
                || unitSo.name.Contains("Worker", System.StringComparison.OrdinalIgnoreCase));

        private static BaseBuilding FindBuildingByName(AIWorldStateSnapshot snapshot, string displayName)
        {
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO != null && building.BuildingSO.Name == displayName)
                {
                    return building;
                }
            }

            return null;
        }
    }

    /// <summary>Tham số base đã resolve — read-only cho một tick.</summary>
    public readonly struct AIBaseRuntimeConfig
    {
        public BuildUnitCommand TrainWorkerCommand { get; }
        public BuildBuildingCommand CorralBuildCommand { get; }
        public BuildBuildingCommand ForgeBuildCommand { get; }
        public BuildBuildingCommand StoreHouseBuildCommand { get; }
        public BuildBuildingCommand BarrackBuildCommand { get; }
        public BuildBuildingCommand DefenseTowerBuildCommand { get; }
        public ResearchUpgradeCommand ResearchUpgradeCommand { get; }
        public int TargetWorkerCount { get; }
        public int ReserveStoneBeforeTrain { get; }
        public int ReserveWoodBeforeTrain { get; }
        public int ReserveFoodBeforeTrain { get; }
        public int ResearchSurplusMin { get; }
        public int PlacementSearchRings { get; }
        public float PlacementSearchStep { get; }
        public float MaxPlacementFootprint { get; }
        public bool RequireBackboneBeforeBarrackAndTower { get; }
        public float PlacementNavMeshObstacleProbeRadius { get; }
        public LayerMask PlacementNavMeshObstacleProbeLayers { get; }

        public AIBaseRuntimeConfig(
            BuildUnitCommand trainWorkerCommand,
            BuildBuildingCommand corralBuildCommand,
            BuildBuildingCommand forgeBuildCommand,
            BuildBuildingCommand storeHouseBuildCommand,
            BuildBuildingCommand barrackBuildCommand,
            BuildBuildingCommand defenseTowerBuildCommand,
            ResearchUpgradeCommand researchUpgradeCommand,
            int targetWorkerCount,
            int reserveStoneBeforeTrain,
            int reserveWoodBeforeTrain,
            int reserveFoodBeforeTrain,
            int researchSurplusMin,
            int placementSearchRings,
            float placementSearchStep,
            float maxPlacementFootprint,
            bool requireBackboneBeforeBarrackAndTower,
            float placementNavMeshObstacleProbeRadius,
            LayerMask placementNavMeshObstacleProbeLayers)
        {
            TrainWorkerCommand = trainWorkerCommand;
            CorralBuildCommand = corralBuildCommand;
            ForgeBuildCommand = forgeBuildCommand;
            StoreHouseBuildCommand = storeHouseBuildCommand;
            BarrackBuildCommand = barrackBuildCommand;
            DefenseTowerBuildCommand = defenseTowerBuildCommand;
            ResearchUpgradeCommand = researchUpgradeCommand;
            TargetWorkerCount = targetWorkerCount;
            ReserveStoneBeforeTrain = reserveStoneBeforeTrain;
            ReserveWoodBeforeTrain = reserveWoodBeforeTrain;
            ReserveFoodBeforeTrain = reserveFoodBeforeTrain;
            ResearchSurplusMin = researchSurplusMin;
            PlacementSearchRings = placementSearchRings;
            PlacementSearchStep = placementSearchStep;
            MaxPlacementFootprint = maxPlacementFootprint;
            RequireBackboneBeforeBarrackAndTower = requireBackboneBeforeBarrackAndTower;
            PlacementNavMeshObstacleProbeRadius = placementNavMeshObstacleProbeRadius;
            PlacementNavMeshObstacleProbeLayers = placementNavMeshObstacleProbeLayers;
        }
    }

    /// <summary>
    /// Command SO trên <see cref="AIController"/> — gán Inspector là dùng; để trống thì quét prefab/map.
    /// Số liệu (target worker, surplus, placement rings…) luôn tự tính trong <see cref="AIBaseConfigResolver"/>.
    /// </summary>
    [System.Serializable]
    public sealed class AIBaseSettings
    {
        [Header("Command assets (Inspector)")]
        [Tooltip("Để trống = tự tìm Build Worker trên Civil Central.")]
        [SerializeField] private BuildUnitCommand trainWorkerCommand;
        [Tooltip("Để trống = tự tìm trên worker AvailableCommands.")]
        [SerializeField] private BuildBuildingCommand buildCorralCommand;
        [SerializeField] private BuildBuildingCommand buildForgeCommand;
        [SerializeField] private BuildBuildingCommand buildStoreHouseCommand;
        [SerializeField] private BuildBuildingCommand buildBarrackCommand;
        [SerializeField] private BuildBuildingCommand buildDefenseTowerCommand;
        [Tooltip("Để trống = tự tìm trên Forge đã xây.")]
        [SerializeField] private ResearchUpgradeCommand researchUpgradeCommand;

        [Header("Giới hạn sinh worker (giai đoạn)")]
        [Tooltip("Chưa có Store House. 0 = dùng cap tự tính từ population.")]
        [SerializeField] private int maxWorkersUntilStoreBuilt = 3;
        [Tooltip("Đã có Store, chưa có Corral. 0 = dùng cap tự tính.")]
        [SerializeField] private int maxWorkersUntilCorralBuilt = 4;
        [Tooltip("Đã có Corral, chưa có Forge. 0 = dùng cap tự tính.")]
        [SerializeField] private int maxWorkersUntilForgeBuilt = 6;
        [Tooltip("Sau khi có Forge. 0 = cap tự tính (~38% pop limit).")]
        [SerializeField] private int maxWorkersTotal;

        [Header("Dành tài nguyên trước khi train worker")]
        [SerializeField] private int reserveStoneBeforeTrain = 80;
        [SerializeField] private int reserveWoodBeforeTrain = 80;
        [SerializeField] private int reserveFoodBeforeTrain = 40;

        [Header("Đặt nhà (ghost — khớp PlayerInput)")]
        [Tooltip("Bán kính > 0: probe NavMeshObstacle như ghost UI. 0 = chỉ Restrictions.")]
        [SerializeField] private float placementNavMeshObstacleProbeRadius;
        [SerializeField] private LayerMask placementNavMeshObstacleProbeLayers = ~0;

        [Header("Barrack/Tower (test)")]
        [Tooltip("Bật = Barrack/Tower chỉ sau Store+Corral+Forge. Tắt = dễ test Barrack/Tower sớm.")]
        [SerializeField] private bool requireBackboneBeforeBarrackAndTower;

        public BuildUnitCommand TrainWorkerCommand => trainWorkerCommand;
        public BuildBuildingCommand BuildCorralCommand => buildCorralCommand;
        public BuildBuildingCommand BuildForgeCommand => buildForgeCommand;
        public BuildBuildingCommand BuildStoreHouseCommand => buildStoreHouseCommand;
        public BuildBuildingCommand BuildBarrackCommand => buildBarrackCommand;
        public BuildBuildingCommand BuildDefenseTowerCommand => buildDefenseTowerCommand;
        public ResearchUpgradeCommand ResearchUpgradeCommand => researchUpgradeCommand;
        public int MaxWorkersUntilStoreBuilt => maxWorkersUntilStoreBuilt;
        public int MaxWorkersUntilCorralBuilt => maxWorkersUntilCorralBuilt;
        public int MaxWorkersUntilForgeBuilt => maxWorkersUntilForgeBuilt;
        public int MaxWorkersTotal => maxWorkersTotal;
        public int ReserveStoneBeforeTrain => reserveStoneBeforeTrain;
        public int ReserveWoodBeforeTrain => reserveWoodBeforeTrain;
        public int ReserveFoodBeforeTrain => reserveFoodBeforeTrain;
        public bool RequireBackboneBeforeBarrackAndTower => requireBackboneBeforeBarrackAndTower;
        public float PlacementNavMeshObstacleProbeRadius => placementNavMeshObstacleProbeRadius;
        public LayerMask PlacementNavMeshObstacleProbeLayers => placementNavMeshObstacleProbeLayers;

        public static AIBaseSettings Default => new();
    }
}
