using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Gắn trên Archer. Mỗi lần bắn spawn prefab mũi tên mới, bay tới đích rồi Destroy.
    /// </summary>
    public sealed class HomingArrowFlight : MonoBehaviour
    {
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float speed = 12f;
        [SerializeField] private float stopDistance = 0.35f;
        [SerializeField] private Vector3 flightRotationOffsetEuler = new(-90f, 0f, 0f);

        public event Action<GameObject> TargetReached;

        private readonly List<GameObject> activeProjectiles = new();

        /// <summary>
        /// Mục tiêu: Spawn mũi tên mới và bay tới target (không dùng lại arrow trên cung).
        /// Cách hoạt động: Instantiate prefab tại firePoint → coroutine MoveTowards → Destroy khi tới.
        /// </summary>
        public void Launch(GameObject target)
        {
            if (target == null || arrowPrefab == null || !gameObject.scene.IsValid())
            {
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;
            GameObject projectile = Instantiate(arrowPrefab, spawnPosition, Quaternion.identity);
            activeProjectiles.Add(projectile);

            float flightHeight = spawnPosition.y;
            Vector3 horizontalGoal = ToHorizontalPoint(target.transform.position, flightHeight);
            ApplyFlightRotation(projectile.transform, horizontalGoal);
            StartCoroutine(FlyRoutine(projectile.transform, target, flightHeight));
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

        private IEnumerator FlyRoutine(Transform projectile, GameObject target, float flightHeight)
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
                ApplyFlightRotation(projectile, goal);

                yield return null;
            }

            GameObject hitTarget = target;
            TargetReached?.Invoke(hitTarget);

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

        private void ApplyFlightRotation(Transform projectile, Vector3 goal)
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

        private void OnDestroy()
        {
            CancelFlight();
        }
    }
}
