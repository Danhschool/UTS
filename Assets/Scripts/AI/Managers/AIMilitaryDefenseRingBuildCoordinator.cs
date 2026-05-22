using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Đặt tháp trên vòng mỗi tick; nhịp 5 phút chỉ mở khóa thêm vòng bán kính.
    /// </summary>
    public static class AIMilitaryDefenseRingBuildCoordinator
    {
        /// <summary>
        /// Mục tiêu: Thử đặt tháp trên vòng đã mở khóa mỗi tick; 5 phút chỉ mở thêm vòng bán kính.
        /// </summary>
        public static void EnqueueDefenseRingIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config,
            AIBaseSettings baseSettings,
            AIMilitarySettings settings,
            Vector3 ccPosition,
            in AIInfluenceMapTickContext influence,
            int maxTowerAttemptsPerTick)
        {
            if (snapshot == null
                || queue == null
                || settings == null
                || !settings.EnableDefenseRingExpansion
                || config.DefenseTowerBuildCommand == null)
            {
                return;
            }

            if (AIMilitaryExpansionPlanner.CountOperationalBarracks(snapshot) < 1)
            {
                return;
            }

            AIMilitaryDefenseRingPlanner.TickRingRadiusExpansion(snapshot.Owner, settings);

            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(ccPosition, influence);

            int attempts = 0;
            int placed = 0;
            bool anyAffordFail = false;

            while (attempts < maxTowerAttemptsPerTick
                && !AIMilitaryDefenseRingPlanner.HasReachedMaxRings(settings, snapshot.Owner))
            {
                attempts++;

                if (!AIMilitaryDefenseRingTowerArmyGate.CanPlaceNextDefenseTower(snapshot, settings))
                {
                    break;
                }

                if (AIInfraBuildUtility.HasPendingInfraBuild(
                        snapshot,
                        AIInfraBuildUtility.DefenseTowerDisplayName))
                {
                    break;
                }

                BuildBuildingCommand towerCommand = config.DefenseTowerBuildCommand;
                if (!SupplyAffordability.HasEnough(snapshot.Owner, towerCommand.Building.Cost))
                {
                    anyAffordFail = true;
                    break;
                }

                if (!AIMilitaryDefenseRingPlanner.TryGetCurrentTowerSlot(
                        snapshot.Owner,
                        out int ringIndex,
                        out int slotIndex))
                {
                    break;
                }

                float ringRadius = AIMilitaryDefenseRingPlanner.GetRingRadius(settings, ringIndex);
                if (!AIMilitaryDefenseRingPlacementUtility.TryFindPlacementOnRing(
                        towerCommand,
                        ccPosition,
                        ringRadius,
                        slotIndex,
                        settings.TowersPerRing,
                        snapshot.Owner,
                        fieldGrid,
                        config,
                        settings,
                        out Vector3 placement))
                {
                    AIMilitaryDefenseRingPlanner.TryAdvanceTowerSlot(settings, snapshot.Owner, out _, out _);
                    continue;
                }

                if (AIMilitaryDefenseRingPlacementUtility.IsTooCloseToBuildingType(
                        snapshot,
                        placement,
                        AIInfraBuildUtility.DefenseTowerDisplayName,
                        config.TowerLineMinSpacing))
                {
                    AIMilitaryDefenseRingPlanner.TryAdvanceTowerSlot(settings, snapshot.Owner, out _, out _);
                    continue;
                }

                if (!TryEnqueueBuildingAt(
                        snapshot,
                        queue,
                        towerCommand,
                        placement,
                        ccPosition,
                        AIMilitaryPriority.BuildTowerLine,
                        fieldGrid))
                {
                    AIMilitaryDefenseRingPlanner.TryAdvanceTowerSlot(settings, snapshot.Owner, out _, out _);
                    continue;
                }

                AIMilitaryDefenseRingPlanner.TryAdvanceTowerSlot(settings, snapshot.Owner, out _, out _);
                AIMilitaryDefenseRingEconomyRecovery.ClearAffordFailures(snapshot.Owner);
                placed++;
            }

            if (anyAffordFail)
            {
                AIMilitaryDefenseRingEconomyRecovery.RecordAffordFailure(
                    snapshot.Owner,
                    settings.ResourceShortageThresholdForEconomyBoost);
            }
            else if (placed > 0)
            {
                AIMilitaryDefenseRingEconomyRecovery.ClearAffordFailures(snapshot.Owner);
            }

        }

        /// <summary>
        /// Mục tiêu: Thiếu tài nguyên nhiều lần → train worker / xây Corral (một intent/tick).
        /// </summary>
        public static void EnqueueEconomyRecoveryIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIBaseRuntimeConfig baseConfig,
            AIBaseSettings baseSettings,
            AIMilitarySettings settings,
            Vector3 ccPosition)
        {
            if (snapshot == null
                || queue == null
                || !AIMilitaryDefenseRingEconomyRecovery.ShouldRequestEconomyBoost(snapshot.Owner))
            {
                return;
            }

            if (AIMilitaryDefenseRingEconomyRecovery.NeedsCorral(snapshot)
                && AIInfraBuildUtility.CanScheduleInfraBuild(
                    snapshot,
                    AIInfraBuildUtility.CorralDisplayName,
                    maxCount: 1))
            {
                BuildBuildingCommand corral = AIBaseConfigResolver.ResolveBuildCommand(
                    snapshot,
                    baseSettings,
                    AIInfraBuildUtility.CorralDisplayName);
                if (corral != null
                    && SupplyAffordability.HasEnough(snapshot.Owner, corral.Building.Cost)
                    && TryEnqueueBuildingAt(
                        snapshot,
                        queue,
                        corral,
                        ccPosition,
                        ccPosition,
                        AIBasePriority.BuildCorral,
                        PlacementFieldGridContext.FromWorldAnchor(ccPosition)))
                {
                    AIMilitaryDefenseRingEconomyRecovery.ClearBoostRequest(snapshot.Owner);
                    return;
                }
            }

            BuildUnitCommand trainWorker = baseConfig.TrainWorkerCommand;
            if (trainWorker != null
                && snapshot.CivilCentral != null
                && snapshot.Workers.Count < baseConfig.TargetWorkerCount + settings.EconomyBoostExtraWorkerCap
                && SupplyAffordability.HasEnough(snapshot.Owner, trainWorker.Unit.Cost))
            {
                CommandContext ctx = new(
                    snapshot.Owner,
                    snapshot.CivilCentral,
                    AIHitUtility.AtPoint(ccPosition));
                if (!trainWorker.IsLocked(ctx) && trainWorker.IsAvailable(ctx))
                {
                    queue.Enqueue(new AICommandIntent(
                        AIBasePriority.TrainWorker + 5,
                        AIManagerIds.Military,
                        snapshot.CivilCentral,
                        trainWorker,
                        AIHitUtility.AtPoint(ccPosition),
                        mouseButton: MouseButton.Right));
                    AIMilitaryDefenseRingEconomyRecovery.ClearBoostRequest(snapshot.Owner);
                }
            }
        }

        private static bool TryEnqueueBuildingAt(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            BuildBuildingCommand buildCommand,
            Vector3 placement,
            Vector3 ccPosition,
            int priority,
            PlacementFieldGridContext fieldGrid)
        {
            if (!AIInfraBuildUtility.TryPickBuilderWorker(snapshot, ccPosition, out Worker builder, out bool mustStopGather))
            {
                return false;
            }

            AIConstructionAssignment.AssignBuilder(builder.GetInstanceID());
            if (mustStopGather)
            {
                builder.InterruptGatherWorkCycle();
            }

            queue.Enqueue(new AICommandIntent(
                priority,
                AIManagerIds.Military,
                builder,
                buildCommand,
                AIHitUtility.AtPoint(placement),
                mouseButton: MouseButton.Left));

            if (buildCommand.Building != null)
            {
                AIInfraBuildOrderTracker.RegisterOrder(
                    snapshot.Owner,
                    buildCommand.Building.Name,
                    builder.GetInstanceID());
                PlacementFieldSelectionRegistry.RegisterSelectedPlacement(
                    snapshot.Owner,
                    placement,
                    fieldGrid);
            }

            return true;
        }
    }
}
