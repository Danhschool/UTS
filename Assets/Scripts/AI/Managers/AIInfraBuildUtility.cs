using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Thứ tự xây nhà base — Store → Corral → Forge → Barrack → Tower.
    /// </summary>
    public static class AIInfraBuildUtility
    {
        public const string CorralDisplayName = "Corral";
        public const string ForgeDisplayName = "Forge";
        public const string StoreHouseDisplayName = SupplyDepositLocator.StoreHouseDisplayName;
        public const string BarrackDisplayName = "Barrack";
        public const string DefenseTowerDisplayName = "Defense Tower";

        /// <summary>
        /// Mục tiêu: Còn nhà nào trong build order chưa có (kể cả đang xây).
        /// Cách hoạt động: Duyệt Store → Corral → Forge → Barrack → Tower.
        /// </summary>
        public static bool NeedsScheduledBuilding(AIWorldStateSnapshot snapshot, AIBaseSettings settings)
        {
            return TryGetNextScheduledBuild(snapshot, settings, out _, out _, out _);
        }

        /// <summary>
        /// Mục tiêu: Backbone (Store/Corral/Forge) chưa đủ — expand placement / ưu tiên ngắt gather để xây.
        /// </summary>
        public static bool NeedsPrimaryInfra(AIWorldStateSnapshot snapshot) =>
            !HasInfraPresent(snapshot, StoreHouseDisplayName)
            || !HasInfraPresent(snapshot, CorralDisplayName)
            || !HasInfraPresent(snapshot, ForgeDisplayName);

        /// <summary>
        /// Mục tiêu: Lệnh xây tiếp theo trong build order + priority + placement bias.
        /// Cách hoạt động: Store trước; Forge cần Corral; Barrack/Tower cần đủ Store+Corral+Forge.
        /// </summary>
        public static bool TryGetNextScheduledBuild(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings,
            out BuildBuildingCommand buildCommand,
            out int priority,
            out bool preferNearCivilCentral)
        {
            buildCommand = null;
            priority = 0;
            preferNearCivilCentral = false;

            if (snapshot == null)
            {
                return false;
            }

            if (TryScheduleIfMissing(snapshot, settings, StoreHouseDisplayName, AIBasePriority.BuildStoreHouse, true, out buildCommand, out priority, out preferNearCivilCentral))
            {
                return true;
            }

            if (TryScheduleIfMissing(snapshot, settings, CorralDisplayName, AIBasePriority.BuildCorral, true, out buildCommand, out priority, out preferNearCivilCentral))
            {
                return true;
            }

            if (!HasInfraPresent(snapshot, CorralDisplayName))
            {
                return false;
            }

            if (TryScheduleIfMissing(snapshot, settings, ForgeDisplayName, AIBasePriority.BuildForge, false, out buildCommand, out priority, out preferNearCivilCentral))
            {
                return true;
            }

            if (AIForgeResearchTierPlanner.ShouldBlockLateInfraBuild(snapshot))
            {
                return false;
            }

            bool canScheduleBarrackAndTower = !RequiresBackboneBeforeBarrackAndTower(settings)
                || HasBackboneInfraComplete(snapshot);
            if (!canScheduleBarrackAndTower)
            {
                return false;
            }

            if (TryScheduleIfMissing(snapshot, settings, BarrackDisplayName, AIBasePriority.BuildBarrack, false, out buildCommand, out priority, out preferNearCivilCentral))
            {
                return true;
            }

            if (TryScheduleIfMissing(snapshot, settings, DefenseTowerDisplayName, AIBasePriority.BuildDefenseTower, false, out buildCommand, out priority, out preferNearCivilCentral))
            {
                return true;
            }

            return false;
        }

        private static bool TryScheduleIfMissing(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings,
            string buildingDisplayName,
            int buildPriority,
            bool preferNearCc,
            out BuildBuildingCommand buildCommand,
            out int priority,
            out bool preferNearCivilCentral)
        {
            buildCommand = AIBaseConfigResolver.ResolveBuildCommand(snapshot, settings, buildingDisplayName);
            priority = buildPriority;
            preferNearCivilCentral = preferNearCc;

            if (buildCommand == null || HasInfraPresent(snapshot, buildingDisplayName))
            {
                buildCommand = null;
                priority = 0;
                preferNearCivilCentral = false;
                return false;
            }

            return true;
        }

        public static bool NeedsStoreBuild(AIWorldStateSnapshot snapshot) =>
            snapshot != null && !HasInfraPresent(snapshot, StoreHouseDisplayName);

        public static bool NeedsCorralBuild(AIWorldStateSnapshot snapshot) =>
            snapshot != null
            && HasInfraPresent(snapshot, StoreHouseDisplayName)
            && !HasInfraPresent(snapshot, CorralDisplayName);

        public static bool NeedsForgeBuild(AIWorldStateSnapshot snapshot) =>
            snapshot != null
            && HasInfraPresent(snapshot, CorralDisplayName)
            && !HasInfraPresent(snapshot, ForgeDisplayName);

        private static bool RequiresBackboneBeforeBarrackAndTower(AIBaseSettings settings) =>
            settings != null && settings.RequireBackboneBeforeBarrackAndTower;

        public static bool HasBackboneInfraComplete(AIWorldStateSnapshot snapshot) =>
            snapshot != null
            && HasInfraPresent(snapshot, StoreHouseDisplayName)
            && HasInfraPresent(snapshot, CorralDisplayName)
            && HasInfraPresent(snapshot, ForgeDisplayName);

        /// <summary>
        /// Mục tiêu: Đã có / đang xây / đã ra lệnh xây loại nhà này — không schedule trùng.
        /// Cách hoạt động: Map buildings → <see cref="AIInfraBuildOrderTracker"/> → worker blackboard.
        /// </summary>
        public static bool HasInfraPresent(AIWorldStateSnapshot snapshot, string displayName)
        {
            if (snapshot == null || string.IsNullOrEmpty(displayName))
            {
                return false;
            }

            if (AIInfraBuildOrderTracker.HasOrderedBuild(snapshot.Owner, displayName))
            {
                return true;
            }

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO == null || building.BuildingSO.Name != displayName)
                {
                    continue;
                }

                if (building.Progress.State == BuildingProgress.BuildingState.Destroyed)
                {
                    continue;
                }

                return true;
            }

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker != null
                    && worker.TryGetCommittedBuildBuildingName(out string pendingName)
                    && pendingName == displayName)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetNextInfraBuild(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings,
            out BuildBuildingCommand buildCommand) =>
            TryGetNextScheduledBuild(snapshot, settings, out buildCommand, out _, out _);

        public static bool CanAffordAndUnlockInfraBuild(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings settings)
        {
            if (!TryGetNextScheduledBuild(snapshot, settings, out BuildBuildingCommand buildCommand, out _, out _))
            {
                return false;
            }

            if (!SupplyAffordability.HasEnough(snapshot.Owner, buildCommand.Building.Cost))
            {
                return false;
            }

            CommandContext context = new(
                snapshot.Owner,
                snapshot.CivilCentral,
                AIHitUtility.AtPoint(snapshot.CivilCentral.transform.position));
            return !buildCommand.IsLocked(context);
        }

        /// <summary>
        /// Mục tiêu: Chọn worker để AI giao build — kể cả đang gather hoặc Move xong nhưng BT chưa Success.
        /// Cách hoạt động: Rảnh → gather redirect → bất kỳ worker không IsBuilding (không committed thật).
        /// </summary>
        public static bool TryPickBuilderWorker(
            AIWorldStateSnapshot snapshot,
            Vector3 anchor,
            out Worker worker,
            out bool mustStopGatherFirst)
        {
            mustStopGatherFirst = false;
            bool preferRedirectGather = NeedsPrimaryInfra(snapshot);

            if (preferRedirectGather && TryPickGatheringWorkerToRedirect(snapshot, anchor, out worker))
            {
                mustStopGatherFirst = true;
                return true;
            }

            if (TryPickIdleConstructionWorker(snapshot, out worker))
            {
                return true;
            }

            if (!preferRedirectGather && TryPickGatheringWorkerToRedirect(snapshot, anchor, out worker))
            {
                mustStopGatherFirst = true;
                return true;
            }

            worker = TryPickAnyAvailableBuilder(snapshot);
            if (worker != null)
            {
                mustStopGatherFirst = worker.IsInGatherWorkCycle;
                return true;
            }

            worker = null;
            return false;
        }

        private static Worker TryPickAnyAvailableBuilder(AIWorldStateSnapshot snapshot)
        {
            Worker best = null;
            int bestId = int.MaxValue;

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker candidate = snapshot.Workers[i];
                if (candidate == null || candidate.IsCommittedToConstructionWork)
                {
                    continue;
                }

                int id = candidate.GetInstanceID();
                if (id < bestId)
                {
                    bestId = id;
                    best = candidate;
                }
            }

            return best;
        }

        public static bool TryPickIdleConstructionWorker(AIWorldStateSnapshot snapshot, out Worker worker)
        {
            worker = null;
            int bestId = int.MaxValue;

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker candidate = snapshot.Workers[i];
                if (candidate == null
                    || candidate.IsCommittedToConstructionWork
                    || candidate.IsGatheringOrReturning
                    || candidate.HasSupplies)
                {
                    continue;
                }

                int id = candidate.GetInstanceID();
                if (id < bestId)
                {
                    bestId = id;
                    worker = candidate;
                }
            }

            return worker != null;
        }

        /// <summary>
        /// Mục tiêu: Worker đang gather gần CC nhất — chuyển sang build (Stop rồi Build).
        /// Cách hoạt động: IsInGatherWorkCycle; không IsBuilding; score khoảng cách tới anchor.
        /// </summary>
        public static bool TryPickGatheringWorkerToRedirect(
            AIWorldStateSnapshot snapshot,
            Vector3 anchor,
            out Worker worker)
        {
            worker = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker candidate = snapshot.Workers[i];
                if (candidate == null
                    || candidate.IsCommittedToConstructionWork
                    || !candidate.IsInGatherWorkCycle)
                {
                    continue;
                }

                float sqr = (candidate.transform.position - anchor).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                worker = candidate;
            }

            return worker != null;
        }
    }
}
