using GameDevTV.RTS.Commands;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chọn điểm đặt nhà — ưu tiên cell influence an toàn, fallback quét NavMesh + Restrictions.
    /// </summary>
    public static class AIInfluencePlacementUtility
    {
        /// <summary>
        /// Mục tiêu: Điểm đặt hợp lệ gần anchor, bias theo influence (gần/xa CC).
        /// Cách hoạt động: TryGetBestCell → sample NavMesh + Restrictions; không có thì quét vòng.
        /// </summary>
        public static bool TryFindPlacement(
            BuildBuildingCommand buildCommand,
            Vector3 anchorPosition,
            int placementSearchRings,
            float placementSearchStep,
            bool expandSearch,
            bool preferNearAnchor,
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
                if (TryValidatePlacement(buildCommand, influencePoint, placementSearchRings, placementSearchStep, out placement))
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
                out placement);
        }

        private static bool TryValidatePlacement(
            BuildBuildingCommand buildCommand,
            Vector3 candidate,
            int placementSearchRings,
            float placementSearchStep,
            out Vector3 placement)
        {
            if (buildCommand.AllRestrictionsPass(candidate))
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
                out placement);
        }
    }
}
