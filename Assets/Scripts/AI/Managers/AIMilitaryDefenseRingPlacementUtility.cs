using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Điểm đặt Barrack/Tháp trên vòng cố định quanh CC.
    /// </summary>
    public static class AIMilitaryDefenseRingPlacementUtility
    {
        /// <summary>
        /// Mục tiêu: Đặt trên vòng (góc slot); không được thì mở rộng bán kính +20m theo cùng hướng (tối đa một bước vòng).
        /// </summary>
        public static bool TryFindPlacementOnRing(
            BuildBuildingCommand buildCommand,
            Vector3 civilCentralPosition,
            float ringRadius,
            int slotIndex,
            int slotsPerRing,
            Owner placementOwner,
            in PlacementFieldGridContext fieldGrid,
            AIMilitaryRuntimeConfig config,
            AIMilitarySettings settings,
            out Vector3 placement)
        {
            placement = default;
            if (buildCommand == null || slotsPerRing <= 0)
            {
                return false;
            }

            float expansionStep = ResolveRadiusExpansionStep(settings);
            int maxExpansionSteps = ResolveMaxRadiusExpansionSteps(settings, expansionStep);

            for (int expansion = 0; expansion <= maxExpansionSteps; expansion++)
            {
                float tryRadius = ringRadius + expansion * expansionStep;
                if (TryFindPlacementAtRingRadius(
                        buildCommand,
                        civilCentralPosition,
                        tryRadius,
                        slotIndex,
                        slotsPerRing,
                        placementOwner,
                        fieldGrid,
                        config,
                        out placement))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveRadiusExpansionStep(AIMilitarySettings settings) =>
            settings != null
                ? Mathf.Max(5f, settings.RingPlacementRadiusExpansionStep)
                : 20f;

        private static int ResolveMaxRadiusExpansionSteps(AIMilitarySettings settings, float expansionStep)
        {
            if (settings == null)
            {
                return 5;
            }

            float ringBand = Mathf.Max(expansionStep, settings.DefenseRingSpacing);
            return Mathf.Max(1, Mathf.FloorToInt(ringBand / expansionStep));
        }

        private static bool TryFindPlacementAtRingRadius(
            BuildBuildingCommand buildCommand,
            Vector3 civilCentralPosition,
            float ringRadius,
            int slotIndex,
            int slotsPerRing,
            Owner placementOwner,
            in PlacementFieldGridContext fieldGrid,
            AIMilitaryRuntimeConfig config,
            out Vector3 placement)
        {
            placement = default;
            float angle = slotIndex * (Mathf.PI * 2f / slotsPerRing);
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 anchor = civilCentralPosition + direction * ringRadius;

            if (buildCommand.AllRestrictionsPass(anchor)
                && PlacementFieldSelectionRegistry.CanAcceptPlacement(placementOwner, anchor, fieldGrid))
            {
                placement = anchor;
                return true;
            }

            return AIBuildingPlacementUtility.TryFindPlacementExpandingFromAnchor(
                buildCommand,
                anchor,
                config.PlacementSearchRings,
                config.PlacementSearchStep,
                expandSearch: true,
                placementOwner,
                fieldGrid,
                out placement);
        }

        public static bool IsTooCloseToBuildingType(
            AIWorldStateSnapshot snapshot,
            Vector3 candidate,
            string displayName,
            float minSpacing)
        {
            if (snapshot?.Buildings == null || minSpacing <= 0f)
            {
                return false;
            }

            float minSqr = minSpacing * minSpacing;
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO == null
                    || building.BuildingSO.Name != displayName
                    || building.Progress.State == BuildingProgress.BuildingState.Destroyed)
                {
                    continue;
                }

                if ((building.transform.position - candidate).sqrMagnitude < minSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
