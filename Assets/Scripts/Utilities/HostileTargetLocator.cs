using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Finds hostile <see cref="IDamageable"/> targets in range for defensive buildings.
    /// </summary>
    public static class HostileTargetLocator
    {
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        /// <summary>
        /// Mục tiêu: Chọn unit địch gần nhất trong tầm để tháp/building tấn công.
        /// Cách hoạt động: OverlapSphereNonAlloc theo layer; bỏ cùng Owner/Unowned; tùy chọn lọc <see cref="IHideable.IsVisible"/>.
        /// </summary>
        public static bool TryFindClosestHostile(
            Vector3 fromPosition,
            float range,
            Owner friendlyOwner,
            LayerMask damageableLayers,
            bool requireVisible,
            out IDamageable closestHostile)
        {
            closestHostile = null;
            if (range <= 0f)
            {
                return false;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                fromPosition,
                range,
                OverlapBuffer,
                damageableLayers,
                QueryTriggerInteraction.Collide);

            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = OverlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (!collider.TryGetComponent(out IDamageable damageable))
                {
                    damageable = collider.GetComponentInParent<IDamageable>();
                }

                if (!IsHostileTarget(damageable, friendlyOwner, requireVisible))
                {
                    continue;
                }

                float sqrDistance = (damageable.Transform.position - fromPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                closestHostile = damageable;
            }

            return closestHostile != null;
        }

        private static bool IsHostileTarget(IDamageable damageable, Owner friendlyOwner, bool requireVisible)
        {
            if (damageable is Object unityObject && unityObject == null)
            {
                return false;
            }

            if (damageable == null
                || damageable.Transform == null
                || damageable.CurrentHealth <= 0
                || damageable.Owner == friendlyOwner
                || damageable.Owner == Owner.Unowned
                || damageable.Owner == Owner.Invalid)
            {
                return false;
            }

            if (requireVisible
                && damageable is IHideable hideable
                && !hideable.IsVisible)
            {
                return false;
            }

            return true;
        }
    }
}
