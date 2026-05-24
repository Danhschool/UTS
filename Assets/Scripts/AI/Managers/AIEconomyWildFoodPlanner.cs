using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Thiếu food và không có mỏ food visible — worker (hoặc lính rảnh) đi săn WildAnimal.
    /// </summary>
    public static class AIEconomyWildFoodPlanner
    {
        /// <summary>
        /// Mục tiêu: Có cần săn thú không (kho thấp + không mỏ food trong tầm nhìn).
        /// Cách hoạt động: So Food trong Supplies; quét GatherableSupplies loại Food visible.
        /// </summary>
        public static bool NeedsWildFoodHunt(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            AIEconomySettings settings)
        {
            if (snapshot == null || settings == null || !settings.EnableWildFoodHunt)
            {
                return false;
            }

            if (snapshot.Food >= settings.FoodHuntBelowAmount)
            {
                return false;
            }

            return !AIEconomyFoodGatherUtility.HasVisibleFoodGatherNode(snapshot, config);
        }

        /// <summary>
        /// Mục tiêu: Gán Attack lên WildAnimal gần worker/lính rảnh.
        /// Cách hoạt động: Tìm thú visible; Resolve AttackCommand; enqueue intent.
        /// </summary>
        public static int EnqueueHuntIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIEconomyRuntimeConfig config,
            AIEconomySettings settings,
            Vector3 anchor)
        {
            if (!NeedsWildFoodHunt(snapshot, config, settings))
            {
                return 0;
            }

            int maxAssignments = Mathf.Max(1, settings.MaxWildFoodHuntersPerTick);
            int assigned = 0;
            float maxRange = settings.WildFoodHuntMaxDistance;
            float maxRangeSqr = maxRange > 0f ? maxRange * maxRange : float.MaxValue;

            for (int i = 0; i < snapshot.Workers.Count && assigned < maxAssignments; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (!IsEligibleHunter(worker))
                {
                    continue;
                }

                if (!TryFindClosestWildAnimal(
                        snapshot.Owner,
                        anchor,
                        maxRangeSqr,
                        requireVisible: true,
                        out WildAnimal animal))
                {
                    continue;
                }

                if (TryEnqueueHuntForUnit(snapshot, queue, worker, animal))
                {
                    assigned++;
                }
            }

            return assigned;
        }

        /// <summary>
        /// Mục tiêu: Một worker săn một con thú khi không có mỏ food.
        /// </summary>
        public static bool TryEnqueueHuntForUnit(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Worker worker,
            AIEconomyRuntimeConfig config,
            AIEconomySettings settings)
        {
            if (worker == null
                || !NeedsWildFoodHunt(snapshot, config, settings)
                || !IsEligibleHunter(worker))
            {
                return false;
            }

            float maxRange = settings.WildFoodHuntMaxDistance;
            float maxRangeSqr = maxRange > 0f ? maxRange * maxRange : float.MaxValue;
            if (!TryFindClosestWildAnimal(
                    snapshot.Owner,
                    worker.transform.position,
                    maxRangeSqr,
                    requireVisible: true,
                    out WildAnimal animal))
            {
                return false;
            }

            return TryEnqueueHuntForUnit(snapshot, queue, worker, animal);
        }

        public static bool TryEnqueueHuntForUnit(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AbstractUnit hunter,
            WildAnimal animal) =>
            TryEnqueueAttackOnAnimal(snapshot, queue, hunter, animal);

        public static bool HasVisibleFoodGatherNode(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config) =>
            AIEconomyFoodGatherUtility.HasVisibleFoodGatherNode(snapshot, config);

        /// <summary>
        /// Mục tiêu: Thú hoang gần anchor, còn sống, trong tầm nhìn (fair fog).
        /// Cách hoạt động: FindObjectsByType WildAnimal — một lần mỗi tick hunt (giống hostile scanner).
        /// </summary>
        public static bool TryFindClosestWildAnimal(
            Owner viewerOwner,
            Vector3 fromPosition,
            float maxRangeSqr,
            bool requireVisible,
            out WildAnimal closest)
        {
            closest = null;
            float bestSqr = float.MaxValue;
            AISceneEntityCache.EnsureFresh();
            WildAnimal[] animals = AISceneEntityCache.WildAnimals;

            for (int i = 0; i < animals.Length; i++)
            {
                WildAnimal animal = animals[i];
                if (animal == null
                    || animal.CurrentHealth <= 0
                    || requireVisible && !FactionFogQuery.IsVisibleTo(viewerOwner, animal))
                {
                    continue;
                }

                float sqr = (animal.transform.position - fromPosition).sqrMagnitude;
                if (sqr > maxRangeSqr || sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                closest = animal;
            }

            return closest != null;
        }

        private static bool IsEligibleHunter(Worker worker) =>
            worker != null
            && worker.CurrentHealth > 0
            && !worker.IsBuilding
            && !worker.IsCommittedToConstructionWork
            && !worker.HasSupplies
            && !worker.IsGatheringOrReturning
            && ResolveAttackCommand(worker) != null;

        private static bool TryEnqueueAttackOnAnimal(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AbstractUnit hunter,
            WildAnimal animal)
        {
            AttackCommand attack = ResolveAttackCommand(hunter);
            if (attack == null || animal == null)
            {
                return false;
            }

            Collider collider = animal.GetComponent<Collider>()
                ?? animal.GetComponentInChildren<Collider>();
            if (collider == null || !AIHitUtility.TryCreateHit(collider, out RaycastHit hit))
            {
                return false;
            }

            CommandContext context = new(snapshot.Owner, hunter, hit, mouseButton: MouseButton.Right);
            if (!attack.CanHandle(context))
            {
                return false;
            }

            queue.Enqueue(new AICommandIntent(
                AIEconomyPriority.HuntWildAnimalForFood,
                AIManagerIds.Economy,
                hunter,
                attack,
                hit,
                mouseButton: MouseButton.Right));
            return true;
        }

        private static AttackCommand ResolveAttackCommand(AbstractUnit unit)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is AttackCommand attack)
                {
                    return attack;
                }
            }

            return null;
        }

        /// <summary>
        /// Mục tiêu: Nhận diện mỏ food trên map (GatherableSupply) — không dùng cho WildAnimal.
        /// Cách hoạt động: So reference Inspector hoặc tên asset <see cref="ScriptableObject.name"/>.
        /// </summary>
        private static bool IsFoodSupply(AIEconomyRuntimeConfig config, SupplySO supply) =>
            AIEconomyFoodGatherUtility.IsFoodSupply(config, supply);
    }
}
