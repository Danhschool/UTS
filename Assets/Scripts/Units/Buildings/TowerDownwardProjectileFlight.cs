using System;
using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Buildings
{
    /// <summary>
    /// Tower projectile: parabolic arc from fire point to the aim position (DamageableSensor).
    /// </summary>
    public sealed class TowerDownwardProjectileFlight : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float speed = 18f;
        [SerializeField] private float stopDistance = 0.35f;
        [SerializeField] private Vector3 flightRotationOffsetEuler = new(-90f, 0f, 0f);
        [Tooltip("Độ cao đỉnh vòng cung so với đường thẳng spawn → mục tiêu (m).")]
        [SerializeField, Min(0f)] private float arcPeakHeightMeters = 4f;

        public event Action<GameObject> TargetReached;

        private readonly List<GameObject> activeProjectiles = new();

        public void LaunchAt(Vector3 worldGoal, GameObject hitTarget)
        {
            if (hitTarget == null || projectilePrefab == null || !gameObject.scene.IsValid())
            {
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
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

        public void BindProjectilePrefab(GameObject prefab)
        {
            if (prefab != null)
            {
                projectilePrefab = prefab;
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
                    ApplyFlightRotation(projectile, tangent);
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

        private void ApplyFlightRotation(Transform projectile, Vector3 direction)
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
