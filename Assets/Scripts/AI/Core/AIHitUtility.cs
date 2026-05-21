using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Tạo <see cref="RaycastHit"/> cho dispatcher — mirror click người chơi, không UI.
    /// </summary>
    public static class AIHitUtility
    {
        /// <summary>
        /// Mục tiêu: Hit lên collider supply/deposit/building cho <see cref="GameDevTV.RTS.Commands.GatherCommand"/>.
        /// Cách hoạt động: Ray từ trên xuống qua bounds; fallback raycast Physics.
        /// </summary>
        public static bool TryCreateHit(Collider collider, out RaycastHit hit)
        {
            hit = default;
            if (collider == null)
            {
                return false;
            }

            Bounds bounds = collider.bounds;
            Vector3 origin = bounds.center + Vector3.up * Mathf.Max(5f, bounds.extents.y + 2f);
            Ray ray = new(origin, Vector3.down);
            if (collider.Raycast(ray, out hit, origin.y - bounds.min.y + 10f))
            {
                return true;
            }

            if (Physics.Raycast(ray, out hit, 50f))
            {
                return hit.collider == collider;
            }

            hit.point = bounds.center;
            hit.normal = Vector3.up;
            hit.distance = 0f;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Hit đặt nhà — chỉ cần <c>point</c> cho <see cref="GameDevTV.RTS.Commands.BuildBuildingCommand"/>.
        /// Cách hoạt động: Gán point/normal; không cần collider.
        /// </summary>
        public static RaycastHit AtPoint(Vector3 point)
        {
            RaycastHit hit = default;
            hit.point = point;
            hit.normal = Vector3.up;
            hit.distance = 0f;
            return hit;
        }
    }
}
