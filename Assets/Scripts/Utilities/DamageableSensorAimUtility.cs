using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Resolves world aim points on units (DamageableSensor when present).
    /// </summary>
    public static class DamageableSensorAimUtility
    {
        /// <summary>
        /// Mục tiêu: Lấy điểm ngắm trên unit (ưu tiên transform của DamageableSensor).
        /// Cách hoạt động: GetComponentInChildren DamageableSensor; không có thì dùng Transform của IDamageable.
        /// </summary>
        public static bool TryGetSensorAimPosition(IDamageable damageable, out Vector3 aimPosition)
        {
            aimPosition = default;

            if (damageable is Object unityObject && unityObject == null)
            {
                return false;
            }

            Transform damageableTransform = damageable.Transform;
            if (damageableTransform == null)
            {
                return false;
            }

            DamageableSensor sensor = damageableTransform.GetComponentInChildren<DamageableSensor>();
            aimPosition = sensor != null ? sensor.transform.position : damageableTransform.position;
            return true;
        }
    }
}
