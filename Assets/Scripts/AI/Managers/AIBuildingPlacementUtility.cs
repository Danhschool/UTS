using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Tìm điểm đặt nhà cho AI — quét từ anchor, hợp lệ chỉ khi <see cref="BuildBuildingCommand"/> Restrictions pass.
    /// </summary>
    public static class AIBuildingPlacementUtility
    {
        private const int MinRingCount = 8;
        private const int MaxRingCount = 24;
        private const int ExpandedRingBonus = 10;

        /// <summary>
        /// Mục tiêu: Chỗ đặt gần anchor trước; không có thì mở rộng vòng — Restrictions + không trùng field 20m.
        /// Cách hoạt động: Vòng tròn quanh anchor → NavMesh → Restrictions → kiểm tra reserved field.
        /// </summary>
        public static bool TryFindPlacementExpandingFromAnchor(
            BuildBuildingCommand buildCommand,
            Vector3 anchorPosition,
            int placementSearchRings,
            float placementSearchStep,
            bool expandSearch,
            Owner placementOwner,
            in PlacementFieldGridContext fieldGrid,
            out Vector3 placement)
        {
            placement = default;

            if (buildCommand == null)
            {
                return false;
            }

            int ringSteps = Mathf.Clamp(
                placementSearchRings + (expandSearch ? ExpandedRingBonus : 4),
                MinRingCount,
                MaxRingCount);
            float step = Mathf.Max(placementSearchStep, 3f);
            float startRadius = Mathf.Max(step * 0.75f, 4f);

            for (int ring = 0; ring < ringSteps; ring++)
            {
                float radius = ring == 0 ? startRadius : ring * step;
                int samples = ring <= 1 ? Mathf.Max(8, ring * 8 + 8) : Mathf.Max(10, ring * 8);
                float sampleDistance = Mathf.Min(8f + ring * 1.25f, 16f);

                for (int s = 0; s < samples; s++)
                {
                    float angle = s * (360f / samples) * Mathf.Deg2Rad;
                    Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    Vector3 candidate = anchorPosition + offset;

                    if (!TrySampleNavMesh(candidate, sampleDistance, out Vector3 navPoint))
                    {
                        continue;
                    }

                    if (!buildCommand.AllRestrictionsPass(navPoint))
                    {
                        continue;
                    }

                    if (!PlacementFieldSelectionRegistry.CanAcceptPlacement(placementOwner, navPoint, fieldGrid))
                    {
                        continue;
                    }

                    placement = navPoint;
                    return true;
                }
            }

            return false;
        }

        private static bool TrySampleNavMesh(Vector3 candidate, float maxDistance, out Vector3 navPoint)
        {
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                navPoint = hit.position;
                return true;
            }

            navPoint = default;
            return false;
        }
    }
}
