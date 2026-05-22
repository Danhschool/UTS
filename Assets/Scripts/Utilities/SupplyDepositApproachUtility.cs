using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Điểm tiếp cận động quanh Store/Civil Central — mỗi worker một ô trên vòng quanh footprint.
    /// </summary>
    public static class SupplyDepositApproachUtility
    {
        private const int MaxRingSlots = 12;
        private const float DefaultApproachSpacing = 2.5f;
        private const float NavMeshSampleRadius = 4f;

        /// <summary>
        /// Mục tiêu: Vị trí NavMesh riêng cho worker nộp tài nguyên — không chồng tại một ClosestPoint.
        /// Cách hoạt động: Góc slot từ slotSeed + hướng từ worker → rìa bounds → SamplePosition.
        /// </summary>
        public static Vector3 ResolveApproachPosition(
            Vector3 fromWorld,
            GameObject depositBuilding,
            int slotSeed,
            float approachSpacing = DefaultApproachSpacing)
        {
            if (depositBuilding == null)
            {
                return fromWorld;
            }

            if (!CombatTargetGeometryUtility.TryGetTargetBounds(depositBuilding, out Bounds bounds))
            {
                return depositBuilding.transform.position;
            }

            Vector3 center = bounds.center;
            center.y = fromWorld.y;

            Vector3 toWorker = fromWorld - center;
            toWorker.y = 0f;
            if (toWorker.sqrMagnitude < 0.04f)
            {
                toWorker = Vector3.forward;
            }

            toWorker.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toWorker);

            float ringRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) + Mathf.Max(1.5f, approachSpacing);
            int slotIndex = Mathf.Abs(slotSeed) % MaxRingSlots;
            float slotAngle = slotIndex * (Mathf.PI * 2f / MaxRingSlots) + (slotSeed % 7) * 0.11f;
            Vector3 ringOffset = (toWorker * Mathf.Cos(slotAngle) + right * Mathf.Sin(slotAngle)) * ringRadius;
            Vector3 candidate = center + ringOffset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return CombatTargetGeometryUtility.GetClosestPointOnTarget(fromWorld, depositBuilding);
        }
    }
}
