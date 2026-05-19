using System;
using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Gắn trên Archer. Spawn prefab mũi tên; bay vòng cung hoặc homing ngang tùy cấu hình.
    /// </summary>
    public sealed class HomingArrowFlight : MonoBehaviour
    {
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float speed = 12f;
        [SerializeField] private float stopDistance = 0.35f;
        [SerializeField] private Vector3 flightRotationOffsetEuler = new(-90f, 0f, 0f);
        [Tooltip("Bật: bắn vòng cung (lên cao rồi rơi xuống mục tiêu). Tắt: bay ngang homing.")]
        [SerializeField] private bool useArcTrajectory = true;
        [Tooltip("Độ cao đỉnh vòng cung (m) — Archer thường 5–8.")]
        [SerializeField, Min(0f)] private float arcPeakHeightMeters = 6f;

        public event Action<GameObject> TargetReached;

        private readonly List<GameObject> activeProjectiles = new();

        public void Launch(GameObject target)
        {
            if (target == null || arrowPrefab == null || !gameObject.scene.IsValid())
            {
                return;
            }

            if (useArcTrajectory)
            {
                Vector3 aimPosition = target.transform.position;
                if (!target.TryGetComponent(out IDamageable damageable))
                {
                    damageable = target.GetComponentInParent<IDamageable>();
                }

                if (damageable != null)
                {
                    DamageableSensorAimUtility.TryGetSensorAimPosition(damageable, out aimPosition);
                }

                LaunchArc(aimPosition, target);
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            GameObject projectile = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity);
            activeProjectiles.Add(projectile);

            float flightHeight = spawnPosition.y;
            Vector3 horizontalGoal = ToHorizontalPoint(target.transform.position, flightHeight);
            ApplyFlightRotationHorizontal(projectile.transform, horizontalGoal);
            StartCoroutine(FlyHomingRoutine(projectile.transform, target, flightHeight));
        }

        /// <summary>
        /// Mục tiêu: Bắn vòng cung tới điểm ngắm (DamageableSensor).
        /// Cách hoạt động: Dùng <see cref="ProjectileArcMath"/>; mũi tên xoay theo tiếp tuyến đường cong.
        /// </summary>
        public void LaunchArc(Vector3 worldGoal, GameObject hitTarget)
        {
            if (hitTarget == null || arrowPrefab == null || !gameObject.scene.IsValid())
            {
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            GameObject projectile = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity);
            activeProjectiles.Add(projectile);

            float arcLength = ProjectileArcMath.EstimateArcLength(spawnPosition, worldGoal, arcPeakHeightMeters);
            float flightDuration = Mathf.Max(arcLength / speed, 0.05f);
            StartCoroutine(FlyArcRoutine(projectile.transform, spawnPosition, worldGoal, hitTarget, flightDuration));
        }

        public void CancelFlight()
        {
            StopAllCoroutines();

            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                if (activeProjectiles[i] != null)
                {
                    Destroy(activeProjectiles[i]);
                }
            }

            activeProjectiles.Clear();
        }

        public void BindFirePoint(Transform shootPoint)
        {
            if (shootPoint != null)
            {
                firePoint = shootPoint;
            }
        }

        public void BindArrowPrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                arrowPrefab = prefab;
            }
        }

        private IEnumerator FlyArcRoutine(
            Transform projectile,
            Vector3 start,
            Vector3 goal,
            GameObject hitTarget,
            float flightDuration)
        {
            float elapsed = 0f;

            while (projectile != null && elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                Vector3 previousPosition = projectile.position;
                Vector3 nextPosition = ProjectileArcMath.EvaluatePosition(start, goal, t, arcPeakHeightMeters);
                projectile.position = nextPosition;

                Vector3 tangent = nextPosition - previousPosition;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    ApplyFlightRotationArc(projectile, tangent);
                }

                if (Vector3.Distance(nextPosition, goal) <= stopDistance)
                {
                    break;
                }

                yield return null;
            }

            TargetReached?.Invoke(hitTarget);

            if (projectile != null)
            {
                activeProjectiles.Remove(projectile.gameObject);
                Destroy(projectile.gameObject);
            }
        }

        private IEnumerator FlyHomingRoutine(Transform projectile, GameObject target, float flightHeight)
        {
            while (target != null && projectile != null)
            {
                Vector3 goal = ToHorizontalPoint(target.transform.position, flightHeight);
                if (HorizontalDistance(projectile.position, goal) <= stopDistance)
                {
                    break;
                }

                Vector3 nextPosition = Vector3.MoveTowards(projectile.position, goal, speed * Time.deltaTime);
                nextPosition.y = flightHeight;
                projectile.position = nextPosition;
                ApplyFlightRotationHorizontal(projectile, goal);

                yield return null;
            }

            TargetReached?.Invoke(target);

            if (projectile != null)
            {
                activeProjectiles.Remove(projectile.gameObject);
                Destroy(projectile.gameObject);
            }
        }

        private static Vector3 ToHorizontalPoint(Vector3 worldPosition, float height) =>
            new(worldPosition.x, height, worldPosition.z);

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float deltaX = to.x - from.x;
            float deltaZ = to.z - from.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private void ApplyFlightRotationHorizontal(Transform projectile, Vector3 goal)
        {
            Vector3 direction = goal - projectile.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            projectile.rotation = lookRotation * Quaternion.Euler(flightRotationOffsetEuler);
        }

        private void ApplyFlightRotationArc(Transform projectile, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            projectile.rotation = lookRotation * Quaternion.Euler(flightRotationOffsetEuler);
        }

        private void OnDestroy()
        {
            CancelFlight();
        }
    }
}
