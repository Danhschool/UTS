using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Train worker tại Civil Central; queue Store → Corral → Forge → Barrack → Tower; research Forge.
    /// </summary>
    public sealed class AIBaseManager
    {
        private const int MaxBuildingQueueSize = 5;

        private readonly AIBaseSettings manualOverrides;
        private readonly List<ResearchUpgradeCommand> researchRoundScratch = new(8);

        public AIBaseManager(AIBaseSettings manualOverrides = null)
        {
            this.manualOverrides = manualOverrides ?? AIBaseSettings.Default;
        }

        /// <summary>
        /// Mục tiêu: Sinh intent base (train, build infra, research) cho tick hiện tại.
        /// Cách hoạt động: Resolve config → train → một nhà theo build order khi đủ điều kiện.
        /// </summary>
        public void EnqueueIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            in AIInfluenceMapTickContext influence = default,
            AIDifficultyRuntimeOverlay difficulty = default)
        {
            if (snapshot == null || queue == null || snapshot.CivilCentral == null)
            {
                return;
            }

            AIBaseRuntimeConfig config = AIBaseConfigResolver.Resolve(snapshot, manualOverrides, difficulty);
            Vector3 ccPosition = snapshot.CivilCentral.transform.position;

            EnqueueTrainWorkerIfNeeded(snapshot, queue, config);
            EnqueueNextScheduledBuildingIfNeeded(snapshot, queue, config, ccPosition, influence);
            EnqueueForgeResearchIfNeeded(snapshot, queue, config);
        }

        /// <summary>
        /// Mục tiêu: Xây đúng một nhà tiếp theo trong build order khi đủ tiền + unlock + chỗ đặt (Restrictions).
        /// Cách hoạt động: TryGetNextScheduledBuild → TryEnqueueInfraBuild (một worker).
        /// </summary>
        private void EnqueueNextScheduledBuildingIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIBaseRuntimeConfig config,
            Vector3 ccPosition,
            in AIInfluenceMapTickContext influence)
        {
            if (!AIInfraBuildUtility.TryGetNextScheduledBuild(
                    snapshot,
                    manualOverrides,
                    out BuildBuildingCommand buildCommand,
                    out int priority,
                    out bool preferNearCivilCentral))
            {
                return;
            }

            TryEnqueueInfraBuild(
                snapshot,
                queue,
                buildCommand,
                config,
                ccPosition,
                priority,
                preferNearCivilCentral,
                influence);
        }

        private void EnqueueTrainWorkerIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIBaseRuntimeConfig config)
        {
            BuildUnitCommand trainCommand = config.TrainWorkerCommand;
            if (trainCommand == null)
            {
                return;
            }

            if (snapshot.Workers.Count >= config.TargetWorkerCount)
            {
                return;
            }

            if (!HasResourceReserveForTrain(snapshot, config, trainCommand))
            {
                return;
            }

            BaseBuilding cc = snapshot.CivilCentral;
            CommandContext context = new(snapshot.Owner, cc, AIHitUtility.AtPoint(cc.transform.position));
            if (trainCommand.IsLocked(context) || !trainCommand.IsAvailable(context))
            {
                return;
            }

            queue.Enqueue(new AICommandIntent(
                AIBasePriority.TrainWorker,
                AIManagerIds.Base,
                cc,
                trainCommand,
                AIHitUtility.AtPoint(cc.transform.position),
                mouseButton: MouseButton.Right));
        }

        /// <summary>
        /// Mục tiêu: Research xen kẽ theo tier (level 1 → economy → level 2 → …).
        /// Cách hoạt động: <see cref="AIForgeResearchTierPlanner"/>; chỉ enqueue upgrade đúng tier đang active.
        /// </summary>
        private void EnqueueForgeResearchIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIBaseRuntimeConfig config)
        {
            AIForgeResearchTierPlanner.TickPhase(snapshot);

            if (!AIForgeResearchTierPlanner.ShouldEnqueueResearchThisTick(snapshot, out int activeTier)
                || !AIForgeResearchRoundUtility.TryGetOperationalForge(snapshot, out BaseBuilding forge))
            {
                return;
            }

            int queueSlots = MaxBuildingQueueSize - forge.QueueSize;
            if (queueSlots <= 0)
            {
                return;
            }

            if (manualOverrides?.ResearchUpgradeCommand != null)
            {
                EnqueueSingleForgeResearch(
                    snapshot,
                    queue,
                    forge,
                    config.ResearchUpgradeCommand,
                    AIBasePriority.ResearchUpgradeRound);
                return;
            }

            int collected = AIForgeResearchTierPlanner.CollectAffordableResearchForTier(
                forge,
                snapshot.Owner,
                activeTier,
                researchRoundScratch,
                queueSlots);

            if (collected == 0)
            {
                return;
            }

            RaycastHit forgeHit = AIHitUtility.AtPoint(forge.transform.position);
            for (int i = 0; i < collected; i++)
            {
                int intentPriority = AIBasePriority.ResearchUpgradeRound - i;
                queue.Enqueue(new AICommandIntent(
                    intentPriority,
                    AIManagerIds.Base,
                    forge,
                    researchRoundScratch[i],
                    forgeHit,
                    mouseButton: MouseButton.Right));
            }
        }

        private void EnqueueSingleForgeResearch(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            BaseBuilding forge,
            ResearchUpgradeCommand research,
            int priority)
        {
            if (research == null || forge.QueueSize >= MaxBuildingQueueSize)
            {
                return;
            }

            CommandContext context = new(snapshot.Owner, forge, AIHitUtility.AtPoint(forge.transform.position));
            if (research.IsLocked(context) || !research.IsAvailable(context))
            {
                return;
            }

            if (research.Upgrade?.Cost != null
                && !SupplyAffordability.HasEnough(snapshot.Owner, research.Upgrade.Cost))
            {
                return;
            }

            queue.Enqueue(new AICommandIntent(
                priority,
                AIManagerIds.Base,
                forge,
                research,
                AIHitUtility.AtPoint(forge.transform.position),
                mouseButton: MouseButton.Right));
        }

        private bool TryEnqueueInfraBuild(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            BuildBuildingCommand buildCommand,
            AIBaseRuntimeConfig config,
            Vector3 ccPosition,
            int priority,
            bool preferNearCivilCentral,
            in AIInfluenceMapTickContext influence)
        {
            if (!SupplyAffordability.HasEnough(snapshot.Owner, buildCommand.Building.Cost))
            {
                return false;
            }

            CommandContext lockContext = new(
                snapshot.Owner,
                snapshot.CivilCentral,
                AIHitUtility.AtPoint(ccPosition));
            if (buildCommand.IsLocked(lockContext))
            {
                return false;
            }

            if (!TryFindPlacementFromCivilCentral(
                    buildCommand,
                    ccPosition,
                    config,
                    preferNearCivilCentral,
                    influence,
                    expandSearch: false,
                    out Vector3 placement)
                && !TryFindPlacementFromCivilCentral(
                    buildCommand,
                    ccPosition,
                    config,
                    preferNearCivilCentral,
                    influence,
                    expandSearch: true,
                    out placement))
            {
                return false;
            }

            return TryEnqueueInfraBuildWithPlacement(
                snapshot,
                queue,
                buildCommand,
                ccPosition,
                priority,
                placement);
        }

        private bool TryEnqueueInfraBuildWithPlacement(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            BuildBuildingCommand buildCommand,
            Vector3 ccPosition,
            int priority,
            Vector3 placement)
        {

            if (!TryResolveBuilderWorker(snapshot, ccPosition, out Worker builder, out bool mustStopGatherFirst))
            {
                return false;
            }

            AIConstructionAssignment.AssignBuilder(builder.GetInstanceID());

            if (mustStopGatherFirst)
            {
                builder.InterruptGatherWorkCycle();
                EnqueueStopGathererForBuild(snapshot, queue, builder);
            }

            queue.Enqueue(new AICommandIntent(
                priority,
                AIManagerIds.Base,
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
            }

            return true;
        }

        private static bool TryResolveBuilderWorker(
            AIWorldStateSnapshot snapshot,
            Vector3 civilCentralPosition,
            out Worker builder,
            out bool mustStopGatherFirst) =>
            AIInfraBuildUtility.TryPickBuilderWorker(snapshot, civilCentralPosition, out builder, out mustStopGatherFirst);

        private static void EnqueueStopGathererForBuild(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Worker gatherer)
        {
            StopCommand stop = ResolveStopCommand(gatherer);
            if (stop == null)
            {
                gatherer.Stop();
                return;
            }

            queue.Enqueue(new AICommandIntent(
                AIBasePriority.ReleaseWorkerForInfraBuild,
                AIManagerIds.Base,
                gatherer,
                stop,
                AIHitUtility.AtPoint(gatherer.transform.position),
                mouseButton: MouseButton.Left));
        }

        /// <summary>
        /// Mục tiêu: Điểm đặt quanh Civil Central — chỉ pass khi Restrictions trên lệnh build cho phép.
        /// Cách hoạt động: <see cref="AIBuildingPlacementUtility.TryFindPlacementExpandingFromAnchor"/>.
        /// </summary>
        private static bool TryFindPlacementFromCivilCentral(
            BuildBuildingCommand buildCommand,
            Vector3 civilCentralPosition,
            AIBaseRuntimeConfig config,
            bool preferNearCivilCentral,
            in AIInfluenceMapTickContext influence,
            bool expandSearch,
            out Vector3 placement) =>
            AIInfluencePlacementUtility.TryFindPlacement(
                buildCommand,
                civilCentralPosition,
                config.PlacementSearchRings,
                config.PlacementSearchStep,
                expandSearch,
                preferNearCivilCentral,
                influence,
                out placement);

        private static StopCommand ResolveStopCommand(Worker worker)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is StopCommand stop)
                {
                    return stop;
                }
            }

            return null;
        }

        private static bool HasResourceReserveForTrain(
            AIWorldStateSnapshot snapshot,
            AIBaseRuntimeConfig config,
            BuildUnitCommand trainCommand)
        {
            if (trainCommand?.Unit?.Cost == null)
            {
                return true;
            }

            int bootstrapTarget = Mathf.Min(2, config.TargetWorkerCount);
            if (snapshot.Workers.Count < bootstrapTarget)
            {
                return SupplyAffordability.HasEnough(snapshot.Owner, trainCommand.Unit.Cost);
            }

            SupplyCostSO cost = trainCommand.Unit.Cost;
            return snapshot.Stone >= cost.Stone + config.ReserveStoneBeforeTrain
                && snapshot.Wood >= cost.Wood + config.ReserveWoodBeforeTrain
                && snapshot.Food >= cost.Food + config.ReserveFoodBeforeTrain;
        }

    }
}
