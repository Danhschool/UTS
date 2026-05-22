using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chọn điểm đặt nhà — ưu tiên cell influence an toàn, fallback quét NavMesh + Restrictions + field 20m.
    /// </summary>
    public static class AIInfluencePlacementUtility
    {
        /// <summary>
        /// Mục tiêu: Điểm đặt hợp lệ gần anchor, bias influence; không trùng điểm đã chọn trong cùng field.
        /// Cách hoạt động: TryGetBestCell → validate; không có thì quét vòng với Restrictions + field check.
        /// </summary>
        public static bool TryFindPlacement(
            BuildBuildingCommand buildCommand,
            Vector3 anchorPosition,
            int placementSearchRings,
            float placementSearchStep,
            bool expandSearch,
            bool preferNearAnchor,
            Owner placementOwner,
            in PlacementFieldGridContext fieldGrid,
            in AIInfluenceMapTickContext influence,
            out Vector3 placement)
        {
            placement = default;
            if (buildCommand == null)
            {
                return false;
            }

            float nearWeight = preferNearAnchor ? 0.12f : -0.04f;
            if (influence.IsValid
                && influence.Map.TryGetBestCell(anchorPosition, nearWeight, out Vector3 influencePoint, out _))
            {
                if (TryValidatePlacement(
                        buildCommand,
                        influencePoint,
                        placementSearchRings,
                        placementSearchStep,
                        placementOwner,
                        fieldGrid,
                        out placement))
                {
                    return true;
                }
            }

            return AIBuildingPlacementUtility.TryFindPlacementExpandingFromAnchor(
                buildCommand,
                anchorPosition,
                placementSearchRings,
                placementSearchStep,
                expandSearch,
                placementOwner,
                fieldGrid,
                out placement);
        }

        private static bool TryValidatePlacement(
            BuildBuildingCommand buildCommand,
            Vector3 candidate,
            int placementSearchRings,
            float placementSearchStep,
            Owner placementOwner,
            in PlacementFieldGridContext fieldGrid,
            out Vector3 placement)
        {
            if (buildCommand.AllRestrictionsPass(candidate)
                && PlacementFieldSelectionRegistry.CanAcceptPlacement(placementOwner, candidate, fieldGrid))
            {
                placement = candidate;
                return true;
            }

            return AIBuildingPlacementUtility.TryFindPlacementExpandingFromAnchor(
                buildCommand,
                candidate,
                Mathf.Max(4, placementSearchRings / 2),
                placementSearchStep,
                expandSearch: false,
                placementOwner,
                fieldGrid,
                out placement);
        }
    }
}
