using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Raycast world thống nhất cho chuột phải / hover cursor — tránh floor che supply (Move thay Gather).
    /// </summary>
    public static class GameplayWorldRaycastUtility
    {
        /// <summary>
        /// Mục tiêu: Chọn hit gần nhất đủ điều kiện (giống <see cref="UnitSelectionHoverCursor"/>).
        /// Cách hoạt động: RaycastAll, sort theo distance, bỏ qua IHideable chưa visible.
        /// </summary>
        public static bool TryGetCommandRaycastHit(Ray ray, LayerMask layers, out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                float.MaxValue,
                layers,
                QueryTriggerInteraction.Collide);

            if (hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Ưu tiên mỏ/building target gần nhất — floor gần camera không được chọn trước supply.
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null)
                {
                    continue;
                }

                if (col.GetComponentInParent<GatherableSupply>() != null
                    || col.GetComponentInParent<IDamageable>() != null)
                {
                    hit = hits[i];
                    return true;
                }
            }

            for (int i = 0; i < hits.Length; i++)
            {
                if (!IsHitEligibleForGameplayCommand(hits[i]))
                {
                    continue;
                }

                hit = hits[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Supply ẩn fog vẫn nhận gather khi click trúng collider mỏ (cursor đã thấy gather).
        /// </summary>
        public static bool IsHitEligibleForGameplayCommand(RaycastHit raycastHit)
        {
            if (raycastHit.collider == null)
            {
                return false;
            }

            if (raycastHit.collider.GetComponentInParent<GatherableSupply>() != null)
            {
                return true;
            }

            IHideable hideable = raycastHit.collider.GetComponentInParent<IHideable>();
            if (hideable == null)
            {
                return true;
            }

            return hideable.IsVisible;
        }
    }
}
