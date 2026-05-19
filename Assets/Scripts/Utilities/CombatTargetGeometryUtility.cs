using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Resolves closest surface points on combat targets (colliders, NavMeshObstacle footprint).
    /// </summary>
    public static class CombatTargetGeometryUtility
    {
        /// <summary>
        /// Mục tiêu: Điểm trên mục tiêu gần attacker nhất (rìa collider / obstacle, không phải tâm pivot).
        /// Cách hoạt động: Gom bounds collider con + NavMeshObstacle, trả về <see cref="Bounds.ClosestPoint"/>.
        /// </summary>
        public static Vector3 GetClosestPointOnTarget(Vector3 fromWorld, GameObject target)
        {
            if (target == null)
            {
                return fromWorld;
            }

            if (TryGetCombinedTargetBounds(target, out Bounds bounds))
            {
                return bounds.ClosestPoint(fromWorld);
            }

            return target.transform.position;
        }

        /// <summary>
        /// Mục tiêu: Khoảng cách từ attacker tới rìa mục tiêu (dùng cho kiểm tra tầm đánh).
        /// Cách hoạt động: <see cref="Vector3.Distance"/> giữa fromWorld và <see cref="GetClosestPointOnTarget"/>.
        /// </summary>
        public static float GetDistanceToTargetSurface(Vector3 fromWorld, GameObject target) =>
            Vector3.Distance(fromWorld, GetClosestPointOnTarget(fromWorld, target));

        private static bool TryGetCombinedTargetBounds(GameObject target, out Bounds combined)
        {
            combined = default;
            bool haveBounds = false;

            foreach (Collider col in target.GetComponentsInChildren<Collider>(true))
            {
                if (!ShouldIncludeCollider(col))
                {
                    continue;
                }

                if (!haveBounds)
                {
                    combined = col.bounds;
                    haveBounds = true;
                }
                else
                {
                    combined.Encapsulate(col.bounds);
                }
            }

            foreach (NavMeshObstacle obstacle in target.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                if (!obstacle.enabled)
                {
                    continue;
                }

                Bounds obstacleBounds = GetNavMeshObstacleWorldBounds(obstacle);
                if (!haveBounds)
                {
                    combined = obstacleBounds;
                    haveBounds = true;
                }
                else
                {
                    combined.Encapsulate(obstacleBounds);
                }
            }

            return haveBounds;
        }

        private static bool ShouldIncludeCollider(Collider col)
        {
            if (col == null || !col.enabled)
            {
                return false;
            }

            if (col.GetComponent<DamageableSensor>() != null)
            {
                return false;
            }

            return true;
        }

        private static Bounds GetNavMeshObstacleWorldBounds(NavMeshObstacle obstacle)
        {
            Transform t = obstacle.transform;

            if (obstacle.shape == NavMeshObstacleShape.Box)
            {
                Vector3 halfExtents = obstacle.size * 0.5f;
                bool first = true;
                Bounds result = default;

                for (int xi = -1; xi <= 1; xi += 2)
                {
                    for (int yi = -1; yi <= 1; yi += 2)
                    {
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 localCorner = obstacle.center + Vector3.Scale(
                                halfExtents,
                                new Vector3(xi, yi, zi));
                            Vector3 worldCorner = t.TransformPoint(localCorner);

                            if (first)
                            {
                                result = new Bounds(worldCorner, Vector3.zero);
                                first = false;
                            }
                            else
                            {
                                result.Encapsulate(worldCorner);
                            }
                        }
                    }
                }

                return result;
            }

            float radius = obstacle.radius * Mathf.Max(
                Mathf.Abs(t.lossyScale.x),
                Mathf.Abs(t.lossyScale.z));
            float height = obstacle.height * Mathf.Abs(t.lossyScale.y);
            Vector3 center = t.TransformPoint(obstacle.center);
            return new Bounds(center, new Vector3(radius * 2f, height, radius * 2f));
        }
    }
}
