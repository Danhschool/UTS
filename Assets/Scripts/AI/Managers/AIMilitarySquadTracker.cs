using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Gộp quân rảnh đứng gần nhau đủ lâu thành squad — di chuyển formation như multi-select.
    /// </summary>
    public sealed class AIMilitarySquadTracker
    {
        private readonly Dictionary<long, float> clusterFirstSeenTime = new(32);
        private readonly List<AbstractUnit> clusterUnitsScratch = new(32);
        private readonly List<int> bfsQueue = new(32);

        /// <summary>
        /// Mục tiêu: Chia danh sách rảnh thành squad (đã gần đủ lâu) và lính đi lẻ.
        /// Cách hoạt động: Cluster theo khoảng cách; key ổn định theo member ids; holdSeconds trước khi gộp.
        /// </summary>
        public void PartitionIdleUnits(
            IReadOnlyList<AbstractUnit> candidates,
            float mergeRadius,
            float holdSeconds,
            int minSquadSize,
            List<List<AbstractUnit>> squadsOut,
            List<AbstractUnit> solosOut)
        {
            squadsOut.Clear();
            solosOut.Clear();
            if (candidates == null || candidates.Count == 0)
            {
                PruneStaleClusters();
                return;
            }

            float mergeSqr = mergeRadius * mergeRadius;
            int count = candidates.Count;
            bool[] visited = new bool[count];
            var seenKeysThisTick = new HashSet<long>();

            for (int seed = 0; seed < count; seed++)
            {
                if (visited[seed])
                {
                    continue;
                }

                clusterUnitsScratch.Clear();
                bfsQueue.Clear();
                bfsQueue.Add(seed);
                visited[seed] = true;

                while (bfsQueue.Count > 0)
                {
                    int current = bfsQueue[bfsQueue.Count - 1];
                    bfsQueue.RemoveAt(bfsQueue.Count - 1);
                    AbstractUnit currentUnit = candidates[current];
                    clusterUnitsScratch.Add(currentUnit);

                    Vector3 currentPos = currentUnit.transform.position;
                    for (int other = 0; other < count; other++)
                    {
                        if (visited[other])
                        {
                            continue;
                        }

                        float sqr = (candidates[other].transform.position - currentPos).sqrMagnitude;
                        if (sqr > mergeSqr)
                        {
                            continue;
                        }

                        visited[other] = true;
                        bfsQueue.Add(other);
                    }
                }

                if (clusterUnitsScratch.Count < minSquadSize)
                {
                    for (int i = 0; i < clusterUnitsScratch.Count; i++)
                    {
                        solosOut.Add(clusterUnitsScratch[i]);
                    }

                    continue;
                }

                long clusterKey = ComputeClusterKey(clusterUnitsScratch);
                seenKeysThisTick.Add(clusterKey);
                if (!clusterFirstSeenTime.TryGetValue(clusterKey, out float firstSeen))
                {
                    clusterFirstSeenTime[clusterKey] = Time.time;
                    for (int i = 0; i < clusterUnitsScratch.Count; i++)
                    {
                        solosOut.Add(clusterUnitsScratch[i]);
                    }

                    continue;
                }

                if (Time.time - firstSeen < holdSeconds)
                {
                    for (int i = 0; i < clusterUnitsScratch.Count; i++)
                    {
                        solosOut.Add(clusterUnitsScratch[i]);
                    }

                    continue;
                }

                List<AbstractUnit> squad = new(clusterUnitsScratch.Count);
                for (int i = 0; i < clusterUnitsScratch.Count; i++)
                {
                    squad.Add(clusterUnitsScratch[i]);
                }

                squadsOut.Add(squad);
            }

            PruneStaleClusters(seenKeysThisTick);
        }

        public void Clear() => clusterFirstSeenTime.Clear();

        private void PruneStaleClusters(HashSet<long> seenKeysThisTick = null)
        {
            if (seenKeysThisTick == null)
            {
                clusterFirstSeenTime.Clear();
                return;
            }

            List<long> remove = null;
            foreach (KeyValuePair<long, float> entry in clusterFirstSeenTime)
            {
                if (!seenKeysThisTick.Contains(entry.Key))
                {
                    remove ??= new List<long>(8);
                    remove.Add(entry.Key);
                }
            }

            if (remove == null)
            {
                return;
            }

            for (int i = 0; i < remove.Count; i++)
            {
                clusterFirstSeenTime.Remove(remove[i]);
            }
        }

        private static long ComputeClusterKey(List<AbstractUnit> units)
        {
            int[] ids = new int[units.Count];
            for (int i = 0; i < units.Count; i++)
            {
                ids[i] = units[i].GetInstanceID();
            }

            System.Array.Sort(ids);
            long hash = 17;
            for (int i = 0; i < ids.Length; i++)
            {
                hash = hash * 31 + ids[i];
            }

            return hash;
        }
    }
}
