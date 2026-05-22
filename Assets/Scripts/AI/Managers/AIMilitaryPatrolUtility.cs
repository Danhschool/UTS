using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Điểm patrol scout — vòng tròn mở rộng dần quanh Civil Central của phe AI (không hướng thẳng CC địch).
    /// </summary>
    public static class AIMilitaryPatrolUtility
    {
        private const float NavMeshSampleDistance = 12f;
        private const int DefaultSectorsPerRing = 8;
        private const float GoldenAngleRadians = 2.39996323f;

        /// <summary>
        /// Mục tiêu: Scout mở map theo vành đai quanh nhà mình, bán kính tăng dần theo phase.
        /// Cách hoạt động: ringIndex = phase / sectors; góc quét 360°; NavMesh sample trên vòng.
        /// </summary>
        public static bool TryGetExpandingRingPatrolDestination(
            Vector3 friendlyCivilCentralPosition,
            int patrolPhase,
            float minRadius,
            float maxRadius,
            float ringStep,
            out Vector3 destination,
            int sectorsPerRing = DefaultSectorsPerRing)
        {
            destination = default;
            if (minRadius < 0f || friendlyCivilCentralPosition == default)
            {
                return false;
            }

            sectorsPerRing = Mathf.Max(4, sectorsPerRing);
            float step = Mathf.Max(4f, ringStep);
            int ringIndex = Mathf.Max(0, patrolPhase / sectorsPerRing);
            int sector = patrolPhase % sectorsPerRing;
            float upperRadius = ResolvePatrolUpperRadiusForRing(ringIndex, minRadius, maxRadius, step);
            float ringRadius = Mathf.Clamp(minRadius + ringIndex * step, minRadius, upperRadius);

            float angle = sector * (Mathf.PI * 2f / sectorsPerRing) + ringIndex * GoldenAngleRadians * 0.15f;
            Vector3 offset = new(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
            Vector3 candidate = friendlyCivilCentralPosition + offset;

            if (TrySamplePatrolPoint(candidate, NavMeshSampleDistance, out destination))
            {
                return true;
            }

            for (int attempt = 1; attempt <= 6; attempt++)
            {
                float jitterAngle = angle + attempt * (Mathf.PI * 2f / sectorsPerRing) * 0.35f;
                float jitterRadius = Mathf.Clamp(ringRadius + attempt * 2f, minRadius, upperRadius + 6f);
                Vector3 jitterOffset = new(
                    Mathf.Cos(jitterAngle) * jitterRadius,
                    0f,
                    Mathf.Sin(jitterAngle) * jitterRadius);
                candidate = friendlyCivilCentralPosition + jitterOffset;
                if (TrySamplePatrolPoint(candidate, NavMeshSampleDistance + attempt * 2f, out destination))
                {
                    return true;
                }
            }

            return NavMesh.SamplePosition(
                friendlyCivilCentralPosition,
                out NavMeshHit fallbackHit,
                upperRadius,
                NavMesh.AllAreas)
                && (destination = fallbackHit.position) != default;
        }

        /// <summary>
        /// Mục tiêu: Patrol ngẫu nhiên (fallback) — vẫn quanh CC ta, không nhắm CC địch.
        /// </summary>
        /// <summary>
        /// Mục tiêu: Một điểm patrol rời trong vòng [minRadius, maxRadius] — seed ổn định theo unit/tick.
        /// </summary>
        public static bool TryGetLoosePatrolDestination(
            Vector3 civilCentralPosition,
            int scatterSeed,
            float minRadius,
            float maxRadius,
            out Vector3 destination)
        {
            destination = default;
            if (civilCentralPosition == default)
            {
                return false;
            }

            float minR = Mathf.Max(4f, minRadius);
            float maxR = Mathf.Max(minR, maxRadius);
            float angle = Mathf.Abs(Mathf.Sin(scatterSeed * 12.9898f)) * (Mathf.PI * 2f);
            float radiusT = Mathf.Abs(Mathf.Sin((scatterSeed + 37) * 78.233f));
            float radius = minR + radiusT * (maxR - minR);
            Vector3 offset = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 candidate = civilCentralPosition + offset;

            if (TrySamplePatrolPoint(candidate, NavMeshSampleDistance, out destination))
            {
                return true;
            }

            for (int attempt = 1; attempt <= 4; attempt++)
            {
                float jitterAngle = angle + attempt * 1.1f;
                float jitterRadius = Mathf.Clamp(radius + attempt * 3f, minR, maxR + 8f);
                Vector3 jitterOffset = new(
                    Mathf.Cos(jitterAngle) * jitterRadius,
                    0f,
                    Mathf.Sin(jitterAngle) * jitterRadius);
                candidate = civilCentralPosition + jitterOffset;
                if (TrySamplePatrolPoint(candidate, NavMeshSampleDistance + attempt * 2f, out destination))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetRandomPatrolDestination(
            Vector3 civilCentralPosition,
            int patrolPhase,
            float minRadius,
            float maxRadius,
            out Vector3 destination,
            float ringStep = 16f) =>
            TryGetExpandingRingPatrolDestination(
                civilCentralPosition,
                patrolPhase,
                minRadius,
                maxRadius,
                ringStep,
                out destination);

        public static float GetCurrentPatrolUpperRadius(
            int patrolPhase,
            float minRadius,
            float maxRadius,
            float ringStep = 16f) =>
            ResolvePatrolUpperRadiusForRing(
                Mathf.Max(0, patrolPhase / DefaultSectorsPerRing),
                minRadius,
                maxRadius,
                ringStep);

        private static float ResolvePatrolUpperRadiusForRing(
            int ringIndex,
            float minRadius,
            float maxRadius,
            float ringStep)
        {
            float step = Mathf.Max(4f, ringStep);
            float expanded = minRadius + (ringIndex + 1) * step;
            if (maxRadius > minRadius)
            {
                return Mathf.Min(expanded, maxRadius);
            }

            return expanded;
        }

        private static bool TrySamplePatrolPoint(Vector3 candidate, float sampleDistance, out Vector3 navPoint)
        {
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                navPoint = hit.position;
                return true;
            }

            navPoint = default;
            return false;
        }

        public static bool IsEligibleForPatrol(AbstractUnit unit) =>
            unit != null && unit is not Worker && unit.IsAvailableForPlannerPatrol();

        public static bool IsEligibleForLoosePatrol(AbstractUnit unit) =>
            unit != null && unit is not Worker && unit.IsEligibleForLoosePatrolMovement();
    }
}
