using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Lập danh sách mỏ gather visible hợp lệ theo loại — một lần mỗi tick economy (không lặp theo worker).
    /// </summary>
    public sealed class AIEconomyGatherSupplyIndex
    {
        readonly struct Candidate
        {
            public readonly GatherableSupply Supply;
            public readonly Collider Collider;
            public readonly bool IsCorpseFood;

            public Candidate(GatherableSupply supply, Collider collider, bool isCorpseFood)
            {
                Supply = supply;
                Collider = collider;
                IsCorpseFood = isCorpseFood;
            }
        }

        readonly List<Candidate> stone = new(32);
        readonly List<Candidate> wood = new(32);
        readonly List<Candidate> food = new(24);

        /// <summary>
        /// Mục tiêu: Xóa và quét snapshot supplies với visibility + territory + classify một lần.
        /// Cách hoạt động: Cache collider trên supply; phân loại Stone/Wood/Food (đánh dấu xác thú).
        /// </summary>
        public void Rebuild(AIWorldStateSnapshot snapshot, AIEconomyRuntimeConfig config)
        {
            stone.Clear();
            wood.Clear();
            food.Clear();

            if (snapshot?.GatherableSupplies == null)
            {
                return;
            }

            Owner owner = snapshot.Owner;
            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply supply = snapshot.GatherableSupplies[i];
                if (supply == null
                    || supply.Amount <= 0
                    || !FactionFogQuery.IsVisibleTo(owner, supply))
                {
                    continue;
                }

                if (!AIEconomyGatherTerritoryGuard.IsGatherSupplyAllowed(snapshot, supply))
                {
                    continue;
                }

                if (!TryResolveCollider(supply, out Collider collider))
                {
                    continue;
                }

                bool isCorpse = AIEconomyWildCorpseFoodUtility.IsWildAnimalCorpseGatherNode(supply);
                switch (Classify(config, supply.Supply))
                {
                    case SupplyKind.Stone:
                        stone.Add(new Candidate(supply, collider, false));
                        break;
                    case SupplyKind.Wood:
                        wood.Add(new Candidate(supply, collider, false));
                        break;
                    case SupplyKind.Food:
                        food.Add(new Candidate(supply, collider, isCorpse));
                        break;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Chọn mỏ gần nhất trong danh sách đã lọc.
        /// Cách hoạt động: Bỏ qua reserved; ưu tiên food không xác khi excludeCorpseFood.
        /// </summary>
        public bool TryPickClosest(
            SupplyKind kind,
            Worker worker,
            Vector3 anchor,
            float anchorWeight,
            bool excludeCorpseFood,
            HashSet<int> reservedSupplyIds,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            bestSupply = null;
            bestCollider = null;
            List<Candidate> list = GetList(kind);
            if (list == null || list.Count == 0)
            {
                return false;
            }

            float bestScore = float.MaxValue;
            Vector3 workerPos = worker.transform.position;

            for (int pass = 0; pass < (kind == SupplyKind.Food && excludeCorpseFood ? 2 : 1); pass++)
            {
                bool corpsesAllowed = pass == 1;
                for (int i = 0; i < list.Count; i++)
                {
                    Candidate candidate = list[i];
                    if (candidate.Supply == null
                        || reservedSupplyIds.Contains(candidate.Supply.GetInstanceID()))
                    {
                        continue;
                    }

                    if (kind == SupplyKind.Food
                        && excludeCorpseFood
                        && candidate.IsCorpseFood != corpsesAllowed)
                    {
                        continue;
                    }

                    Vector3 pos = candidate.Supply.transform.position;
                    float score = (pos - workerPos).sqrMagnitude + (pos - anchor).sqrMagnitude * anchorWeight;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestSupply = candidate.Supply;
                    bestCollider = candidate.Collider;
                }

                if (bestSupply != null || !excludeCorpseFood || kind != SupplyKind.Food)
                {
                    break;
                }
            }

            return bestSupply != null;
        }

        List<Candidate> GetList(SupplyKind kind) =>
            kind switch
            {
                SupplyKind.Stone => stone,
                SupplyKind.Wood => wood,
                SupplyKind.Food => food,
                _ => null
            };

        static SupplyKind Classify(AIEconomyRuntimeConfig config, SupplySO supply)
        {
            AIEconomySupplyKindClassifier.Kind kind = AIEconomySupplyKindClassifier.Classify(
                supply,
                config.StoneSupply,
                config.WoodSupply,
                config.FoodSupply);
            return kind switch
            {
                AIEconomySupplyKindClassifier.Kind.Stone => SupplyKind.Stone,
                AIEconomySupplyKindClassifier.Kind.Wood => SupplyKind.Wood,
                AIEconomySupplyKindClassifier.Kind.Food => SupplyKind.Food,
                _ => SupplyKind.Unknown
            };
        }

        static bool TryResolveCollider(GatherableSupply supply, out Collider collider)
        {
            collider = supply.GetComponent<Collider>();
            if (collider != null)
            {
                return true;
            }

            collider = supply.GetComponentInChildren<Collider>();
            return collider != null;
        }

        public enum SupplyKind
        {
            Unknown = 0,
            Stone = 1,
            Wood = 2,
            Food = 3
        }
    }
}
