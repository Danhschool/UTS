using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units.Formation
{
    /// <summary>
    /// SRP: Tính vị trí đích hình vuông — hàng trước tiến hướng điểm click, archer hàng sau.
    /// </summary>
    public static class UnitSquareFormationPlanner
    {
        private const float MinSpacing = 1.25f;

        /// <summary>
        /// Mục tiêu: Mỗi unit một ô trong lưới vuông quanh <paramref name="destination"/>.
        /// Cách hoạt động: Tách front/back → gán hàng 0 gần đích, hàng cuối cho archer → sample NavMesh.
        /// </summary>
        public static Vector3[] ComputeWorldPositions(
            IReadOnlyList<AbstractUnit> units,
            Vector3 destination,
            float spacingMultiplier)
        {
            if (units == null || units.Count == 0)
            {
                return System.Array.Empty<Vector3>();
            }

            if (units.Count == 1)
            {
                return new[] { SampleNavMeshForSlot(SnapY(destination, units[0]), 0, MinSpacing) };
            }

            float spacing = ResolveSpacing(units, spacingMultiplier);
            Vector3 forward = ResolveForward(units, destination);
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            PartitionUnits(units, out List<AbstractUnit> frontline, out List<AbstractUnit> backline);
            int total = frontline.Count + backline.Count;
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(total)));

            int frontRows = frontline.Count > 0
                ? Mathf.CeilToInt(frontline.Count / (float)cols)
                : 0;
            int backRows = backline.Count > 0
                ? Mathf.CeilToInt(backline.Count / (float)cols)
                : 0;
            int rows = Mathf.Max(1, frontRows + backRows);

            while (frontRows * cols < frontline.Count)
            {
                frontRows++;
                rows = frontRows + backRows;
            }

            while (backRows * cols < backline.Count)
            {
                backRows++;
                rows = frontRows + backRows;
            }

            while (rows * cols < total)
            {
                cols++;
                frontRows = frontline.Count > 0
                    ? Mathf.CeilToInt(frontline.Count / (float)cols)
                    : 0;
                backRows = backline.Count > 0
                    ? Mathf.CeilToInt(backline.Count / (float)cols)
                    : 0;
                rows = Mathf.Max(1, frontRows + backRows);
            }

            Vector3[] results = new Vector3[units.Count];
            List<Vector3> placedSoFar = new List<Vector3>(units.Count);
            for (int i = 0; i < results.Length; i++)
            {
                AbstractUnit unit = units[i];
                Vector3 seed = unit != null
                    ? SnapY(destination + GetSlotOffset(i, spacing), unit)
                    : destination + GetSlotOffset(i, spacing);
                results[i] = SampleNavMeshForSlot(seed, i, spacing, placedSoFar);
                placedSoFar.Add(results[i]);
            }

            Dictionary<AbstractUnit, int> unitToIndex = new(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    unitToIndex[units[i]] = i;
                }
            }

            int frontSlot = 0;
            for (int r = 0; r < frontRows && frontSlot < frontline.Count; r++)
            {
                for (int c = 0; c < cols && frontSlot < frontline.Count; c++)
                {
                    AssignSlot(
                        results,
                        unitToIndex,
                        frontline[frontSlot],
                        destination,
                        forward,
                        right,
                        spacing,
                        r,
                        c,
                        cols);
                    frontSlot++;
                }
            }

            int backRowStart = frontRows;
            int backSlot = 0;
            for (int r = backRowStart; r < backRowStart + backRows && backSlot < backline.Count; r++)
            {
                for (int c = 0; c < cols && backSlot < backline.Count; c++)
                {
                    AssignSlot(
                        results,
                        unitToIndex,
                        backline[backSlot],
                        destination,
                        forward,
                        right,
                        spacing,
                        r,
                        c,
                        cols);
                    backSlot++;
                }
            }

            AssignOverflowNearDestination(
                results,
                unitToIndex,
                frontline,
                frontSlot,
                destination,
                forward,
                right,
                spacing,
                cols);
            AssignOverflowNearDestination(
                results,
                unitToIndex,
                backline,
                backSlot,
                destination,
                forward,
                right,
                spacing,
                cols);

            EnforceMinimumSeparation(results, spacing * 1.05f);
            return results;
        }

        private static void AssignSlot(
            Vector3[] results,
            Dictionary<AbstractUnit, int> unitToIndex,
            AbstractUnit unit,
            Vector3 destination,
            Vector3 forward,
            Vector3 right,
            float spacing,
            int row,
            int col,
            int cols)
        {
            if (unit == null || !unitToIndex.TryGetValue(unit, out int index))
            {
                return;
            }

            Vector3 raw = CellToWorld(destination, forward, right, spacing, row, col, cols);
            List<Vector3> placed = CollectNonDefaultPlaced(results, index);
            results[index] = SampleNavMeshForSlot(SnapY(raw, unit), index, spacing, placed);
        }

        /// <summary>
        /// Mục tiêu: Unit chưa có ô (edge case) vẫn nhận đích gần formation, không Vector3.zero.
        /// </summary>
        private static void AssignOverflowNearDestination(
            Vector3[] results,
            Dictionary<AbstractUnit, int> unitToIndex,
            List<AbstractUnit> roleUnits,
            int assignedCount,
            Vector3 destination,
            Vector3 forward,
            Vector3 right,
            float spacing,
            int cols)
        {
            for (int i = assignedCount; i < roleUnits.Count; i++)
            {
                AbstractUnit unit = roleUnits[i];
                if (unit == null || !unitToIndex.TryGetValue(unit, out int index))
                {
                    continue;
                }

                int overflowIndex = i - assignedCount;
                int col = overflowIndex % cols;
                int extraRow = overflowIndex / cols;
                Vector3 raw = CellToWorld(destination, forward, right, spacing, extraRow, col, cols);
                List<Vector3> placed = CollectNonDefaultPlaced(results, index);
                results[index] = SampleNavMeshForSlot(SnapY(raw, unit), index, spacing, placed);
            }
        }

        private static List<Vector3> CollectNonDefaultPlaced(Vector3[] results, int excludeIndex)
        {
            List<Vector3> placed = new List<Vector3>(results.Length);
            for (int i = 0; i < results.Length; i++)
            {
                if (i != excludeIndex)
                {
                    placed.Add(results[i]);
                }
            }

            return placed;
        }

        /// <summary>
        /// Mục tiêu: Tránh nhiều unit sample NavMesh trùng một điểm hẹp.
        /// Cách hoạt động: Nhiều vòng đẩy XZ + sample lại; tách ô trùng tâm bằng góc vàng.
        /// </summary>
        private static void EnforceMinimumSeparation(Vector3[] positions, float minSeparation)
        {
            if (positions == null || positions.Length < 2 || minSeparation <= 0f)
            {
                return;
            }

            float minSqr = minSeparation * minSeparation;
            const int passes = 4;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    for (int j = i + 1; j < positions.Length; j++)
                    {
                        Vector3 offset = positions[j] - positions[i];
                        offset.y = 0f;
                        if (offset.sqrMagnitude >= minSqr)
                        {
                            continue;
                        }

                        Vector3 pushDir = offset.sqrMagnitude < 0.01f
                            ? GetSlotOffset(j, minSeparation).normalized
                            : offset.normalized;
                        if (pushDir.sqrMagnitude < 0.01f)
                        {
                            pushDir = Vector3.right;
                        }

                        positions[j] = positions[i] + pushDir * minSeparation;
                        List<Vector3> avoid = CollectNonDefaultPlaced(positions, j);
                        positions[j] = SampleNavMeshForSlot(positions[j], j, minSeparation, avoid);
                    }
                }
            }
        }

        private static void PartitionUnits(
            IReadOnlyList<AbstractUnit> units,
            out List<AbstractUnit> frontline,
            out List<AbstractUnit> backline)
        {
            frontline = new List<AbstractUnit>(units.Count);
            backline = new List<AbstractUnit>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                if (UnitFormationRoleClassifier.GetRole(unit) == UnitFormationRole.Backline)
                {
                    backline.Add(unit);
                }
                else
                {
                    frontline.Add(unit);
                }
            }
        }

        private static float ResolveSpacing(IReadOnlyList<AbstractUnit> units, float spacingMultiplier)
        {
            float maxRadius = 0.5f;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                {
                    maxRadius = Mathf.Max(maxRadius, units[i].AgentRadius);
                }
            }

            return Mathf.Max(MinSpacing, maxRadius * 2f * Mathf.Max(1f, spacingMultiplier));
        }

        private static Vector3 ResolveForward(IReadOnlyList<AbstractUnit> units, Vector3 destination)
        {
            Vector3 centroid = Vector3.zero;
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == null)
                {
                    continue;
                }

                centroid += units[i].transform.position;
                count++;
            }

            if (count > 0)
            {
                centroid /= count;
            }

            Vector3 forward = destination - centroid;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.04f)
            {
                forward = Vector3.forward;
            }

            return forward.normalized;
        }

        private static Vector3 CellToWorld(
            Vector3 destination,
            Vector3 forward,
            Vector3 right,
            float spacing,
            int row,
            int col,
            int cols)
        {
            float colOffset = (col - (cols - 1) * 0.5f) * spacing;
            float rowOffset = row * spacing;
            return destination - forward * rowOffset + right * colOffset;
        }

        private static Vector3 SnapY(Vector3 position, AbstractUnit unit)
        {
            if (unit != null)
            {
                position.y = unit.transform.position.y;
            }

            return position;
        }

        /// <summary>
        /// Mục tiêu: Offset cố định theo slot — tránh nhiều unit cùng sample một điểm NavMesh.
        /// </summary>
        private static Vector3 GetSlotOffset(int slotIndex, float spacing)
        {
            float angle = slotIndex * 137.50776405f * Mathf.Deg2Rad;
            float ring = 0.45f + (slotIndex % 5) * 0.12f;
            float radius = spacing * ring;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// Mục tiêu: Sample NavMesh với offset vòng theo slot — không trùng ô đã đặt.
        /// Cách hoạt động: Luôn bias theo slot; thử vòng; bỏ qua điểm quá gần placedSoFar.
        /// </summary>
        private static Vector3 SampleNavMeshForSlot(
            Vector3 position,
            int slotIndex,
            float spacing,
            List<Vector3> placedSoFar = null)
        {
            float sampleRadius = Mathf.Max(3f, spacing * 0.75f);
            float minSeparation = spacing * 0.9f;
            float minSqr = minSeparation * minSeparation;

            Vector3 biased = position + GetSlotOffset(slotIndex, spacing);
            if (TrySampleUniqueNavPoint(biased, sampleRadius, minSqr, placedSoFar, out Vector3 first))
            {
                return first;
            }

            int ring = 1 + slotIndex / 4;
            float angle = slotIndex * 47f * Mathf.Deg2Rad;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 offset = GetSlotOffset(slotIndex + attempt, spacing)
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * spacing * 0.4f);
                Vector3 candidate = position + offset;
                if (TrySampleUniqueNavPoint(candidate, sampleRadius, minSqr, placedSoFar, out Vector3 resolved))
                {
                    return resolved;
                }

                angle += Mathf.PI * 0.45f;
            }

            return biased;
        }

        private static bool TrySampleUniqueNavPoint(
            Vector3 candidate,
            float sampleRadius,
            float minSqr,
            List<Vector3> placedSoFar,
            out Vector3 resolved)
        {
            resolved = candidate;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            resolved = hit.position;
            if (placedSoFar == null || placedSoFar.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < placedSoFar.Count; i++)
            {
                Vector3 delta = resolved - placedSoFar[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
