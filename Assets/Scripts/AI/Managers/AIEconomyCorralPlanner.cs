using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Thiếu food → xây thêm Corral (passive food); không thay thế gather mỏ food.
    /// </summary>
    public static class AIEconomyCorralPlanner
    {
        private static readonly System.Collections.Generic.Dictionary<int, int> LastEnqueuePlannerTickByOwner = new(4);

        /// <summary>
        /// Mục tiêu: Một intent xây Corral khi kho food thấp và còn slot/cost/placement.
        /// Cách hoạt động: Cần Store; cap tổng Corral; cooldown tick để không cướp worker gather mỗi tick.
        /// </summary>
        public static bool TryEnqueueExtraCorral(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIEconomyRuntimeConfig economyConfig,
            AIEconomySettings settings,
            in AIInfluenceMapTickContext influence,
            int plannerTickIndex = 0)
        {
            if (snapshot == null
                || queue == null
                || settings == null
                || !settings.EnableExtraCorralWhenFoodLow
                || snapshot.CivilCentral == null
                || !AIEconomyFoodGatherUtility.IsFoodEconomicallyScarce(snapshot, settings))
            {
                return false;
            }

            int cooldown = Mathf.Max(1, settings.ExtraCorralAttemptIntervalTicks);
            int ownerKey = (int)snapshot.Owner;
            if (LastEnqueuePlannerTickByOwner.TryGetValue(ownerKey, out int lastTick)
                && plannerTickIndex - lastTick < cooldown)
            {
                return false;
            }

            if (!AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.StoreHouseDisplayName))
            {
                return false;
            }

            int maxCorrals = Mathf.Max(1, settings.MaxCorralsForFoodEconomy);
            if (!AIInfraBuildUtility.CanScheduleInfraBuild(
                    snapshot,
                    AIInfraBuildUtility.CorralDisplayName,
                    maxCorrals))
            {
                return false;
            }

            BuildBuildingCommand corral = AIBaseConfigResolver.ResolveBuildCommand(
                snapshot,
                null,
                AIInfraBuildUtility.CorralDisplayName);
            if (corral?.Building == null
                || !SupplyAffordability.HasEnough(snapshot.Owner, corral.Building.Cost))
            {
                return false;
            }

            Vector3 ccPosition = snapshot.CivilCentral.transform.position;
            CommandContext lockContext = new(
                snapshot.Owner,
                snapshot.CivilCentral,
                AIHitUtility.AtPoint(ccPosition));
            if (corral.IsLocked(lockContext))
            {
                return false;
            }

            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(ccPosition, influence);
            if (!AIInfluencePlacementUtility.TryFindPlacement(
                    corral,
                    ccPosition,
                    economyConfig.PlacementSearchRings,
                    economyConfig.PlacementSearchStep,
                    expandSearch: true,
                    preferNearAnchor: true,
                    snapshot.Owner,
                    fieldGrid,
                    influence,
                    out Vector3 placement))
            {
                return false;
            }

            if (!AIInfraBuildUtility.TryPickBuilderWorker(snapshot, ccPosition, out Worker builder, out bool mustStopGather))
            {
                return false;
            }

            AIConstructionAssignment.AssignBuilder(builder.GetInstanceID());
            if (mustStopGather)
            {
                builder.InterruptGatherWorkCycle();
            }

            PlacementFieldSelectionRegistry.RegisterSelectedPlacement(snapshot.Owner, placement, fieldGrid);
            queue.Enqueue(new AICommandIntent(
                AIBasePriority.BuildCorral,
                AIManagerIds.Economy,
                builder,
                corral,
                AIHitUtility.AtPoint(placement),
                mouseButton: MouseButton.Left));

            AIInfraBuildOrderTracker.RegisterOrder(
                snapshot.Owner,
                corral.Building.Name,
                builder.GetInstanceID());

            LastEnqueuePlannerTickByOwner[ownerKey] = plannerTickIndex;
            return true;
        }
    }
}
