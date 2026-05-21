using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Xen kẽ research theo tier (Damage 1, Health 1, …) với phase economy (build/gather).
    /// </summary>
    public static class AIForgeResearchTierPlanner
    {
        private const int DefaultEconomyPhaseTicks = 4;
        private static readonly Regex TrailingTierRegex = new(@"\s(\d+)\s*$", RegexOptions.Compiled);

        private static readonly Dictionary<Owner, ForgeResearchPlanState> statesByOwner = new();

        private enum ForgeMacroPhase
        {
            Research,
            Economy
        }

        private sealed class ForgeResearchPlanState
        {
            public ForgeMacroPhase Phase = ForgeMacroPhase.Research;
            public int ActiveTier = 1;
            public int EconomyTicksRemaining;
            public bool AllTiersComplete;
        }

        /// <summary>
        /// Mục tiêu: Cập nhật Research ↔ Economy mỗi tick AI.
        /// Cách hoạt động: Hết tier hiện tại (không còn upgrade tier đó + queue trống tier) → economy; hết timer → tier kế.
        /// </summary>
        public static void TickPhase(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null
                || !AIForgeResearchRoundUtility.TryGetOperationalForge(snapshot, out BaseBuilding forge))
            {
                return;
            }

            ForgeResearchPlanState state = GetOrCreateState(snapshot.Owner);
            if (state.AllTiersComplete)
            {
                return;
            }

            if (state.Phase == ForgeMacroPhase.Research)
            {
                TickResearchPhase(snapshot.Owner, forge, state);
            }
            else
            {
                TickEconomyPhase(snapshot.Owner, forge, state);
            }
        }

        /// <summary>
        /// Mục tiêu: Barrack/Tower chỉ bị chặn khi đang trong phase research của một tier.
        /// </summary>
        public static bool ShouldBlockLateInfraBuild(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null
                || !AIForgeResearchRoundUtility.TryGetOperationalForge(snapshot, out BaseBuilding forge))
            {
                return false;
            }

            ForgeResearchPlanState state = GetOrCreateState(snapshot.Owner);
            if (state.AllTiersComplete || state.Phase != ForgeMacroPhase.Research)
            {
                return false;
            }

            int tier = state.ActiveTier;
            return ForgeQueueHasTierResearchInProgress(forge, tier)
                || (HasIncompleteTierResearch(forge, snapshot.Owner, tier)
                    && HasEnqueueableTierResearch(forge, snapshot.Owner, tier));
        }

        /// <summary>
        /// Mục tiêu: Có nên enqueue research Forge tick này không.
        /// Cách hoạt động: Phase Research, tier hiện tại còn upgrade enqueue được hoặc queue đang chạy tier đó.
        /// </summary>
        public static bool ShouldEnqueueResearchThisTick(AIWorldStateSnapshot snapshot, out int activeTier)
        {
            activeTier = 0;
            if (snapshot == null
                || !AIForgeResearchRoundUtility.TryGetOperationalForge(snapshot, out BaseBuilding forge))
            {
                return false;
            }

            ForgeResearchPlanState state = GetOrCreateState(snapshot.Owner);
            if (state.AllTiersComplete || state.Phase != ForgeMacroPhase.Research)
            {
                return false;
            }

            activeTier = state.ActiveTier;
            return HasEnqueueableTierResearch(forge, snapshot.Owner, activeTier);
        }

        public static int CollectAffordableResearchForTier(
            BaseBuilding forge,
            Owner owner,
            int tier,
            List<ResearchUpgradeCommand> output,
            int maxCount) =>
            AIForgeResearchRoundUtility.CollectAffordableResearchCommandsForTier(
                forge,
                owner,
                tier,
                output,
                maxCount);

        /// <summary>
        /// Mục tiêu: Lấy số tier từ tên upgrade/command (vd. "… Damage 1" → 1).
        /// Cách hoạt động: Regex số ở cuối chuỗi Name; không khớp → tier 0 (bỏ qua lọc tier).
        /// </summary>
        public static bool TryParseResearchTier(UnlockableSO unlockable, out int tier)
        {
            tier = 0;
            if (unlockable == null)
            {
                return false;
            }

            string name = unlockable.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            Match match = TrailingTierRegex.Match(name);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out tier))
            {
                return false;
            }

            return tier > 0;
        }

        private static void TickResearchPhase(Owner owner, BaseBuilding forge, ForgeResearchPlanState state)
        {
            int tier = state.ActiveTier;
            bool tierQueueBusy = ForgeQueueHasTierResearchInProgress(forge, tier);
            bool tierIncomplete = HasIncompleteTierResearch(forge, owner, tier);
            bool canEnqueueMore = HasEnqueueableTierResearch(forge, owner, tier);

            if (tierIncomplete && (tierQueueBusy || canEnqueueMore))
            {
                return;
            }

            int nextTier = GetLowestIncompleteTier(forge, owner);
            if (nextTier < 0)
            {
                state.AllTiersComplete = true;
                state.Phase = ForgeMacroPhase.Economy;
                return;
            }

            BeginEconomyPhase(state);
        }

        private static void TickEconomyPhase(Owner owner, BaseBuilding forge, ForgeResearchPlanState state)
        {
            if (ForgeQueueHasAnyResearchInProgress(forge))
            {
                return;
            }

            if (state.EconomyTicksRemaining > 0)
            {
                state.EconomyTicksRemaining--;
            }

            if (state.EconomyTicksRemaining > 0)
            {
                return;
            }

            int nextTier = GetLowestIncompleteTier(forge, owner);
            if (nextTier < 0)
            {
                state.AllTiersComplete = true;
                return;
            }

            state.ActiveTier = nextTier;
            state.Phase = ForgeMacroPhase.Research;
        }

        private static void BeginEconomyPhase(ForgeResearchPlanState state)
        {
            state.Phase = ForgeMacroPhase.Economy;
            state.EconomyTicksRemaining = DefaultEconomyPhaseTicks;
        }

        private static bool IsResearchPhaseActiveForTier(BaseBuilding forge, Owner owner, int tier)
        {
            if (tier <= 0)
            {
                return false;
            }

            if (ForgeQueueHasTierResearchInProgress(forge, tier))
            {
                return true;
            }

            if (!HasIncompleteTierResearch(forge, owner, tier))
            {
                return false;
            }

            return HasEnqueueableTierResearch(forge, owner, tier);
        }

        /// <summary>
        /// Mục tiêu: Còn research tier có thể bấm ngay (unlock + available + đủ tiền).
        /// </summary>
        private static bool HasEnqueueableTierResearch(BaseBuilding forge, Owner owner, int tier)
        {
            if (forge?.AvailableCommands == null || tier <= 0)
            {
                return false;
            }

            CommandContext context = new(owner, forge, AIHitUtility.AtPoint(forge.transform.position));
            for (int i = 0; i < forge.AvailableCommands.Length; i++)
            {
                if (forge.AvailableCommands[i] is not ResearchUpgradeCommand research
                    || research.Upgrade == null
                    || !TryParseResearchTier(research.Upgrade, out int upgradeTier)
                    || upgradeTier != tier)
                {
                    continue;
                }

                if (research.IsLocked(context) || !research.IsAvailable(context))
                {
                    continue;
                }

                if (forge.ContainsInQueue(research.Upgrade))
                {
                    continue;
                }

                if (research.Upgrade.Cost != null
                    && !SupplyAffordability.HasEnough(owner, research.Upgrade.Cost))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool HasIncompleteTierResearch(BaseBuilding forge, Owner owner, int tier)
        {
            if (forge?.AvailableCommands == null || forge.BuildingSO?.TechTree == null)
            {
                return false;
            }

            TechTreeSO techTree = forge.BuildingSO.TechTree;
            for (int i = 0; i < forge.AvailableCommands.Length; i++)
            {
                if (forge.AvailableCommands[i] is not ResearchUpgradeCommand research
                    || research.Upgrade == null
                    || !TryParseResearchTier(research.Upgrade, out int upgradeTier)
                    || upgradeTier != tier)
                {
                    continue;
                }

                UpgradeSO upgrade = research.Upgrade;
                if (!techTree.IsUnlocked(owner, upgrade))
                {
                    continue;
                }

                if (upgrade.IsOneTimeUnlock && techTree.IsResearched(owner, upgrade))
                {
                    continue;
                }

                if (forge.ContainsInQueue(upgrade))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static int GetLowestIncompleteTier(BaseBuilding forge, Owner owner)
        {
            int lowest = int.MaxValue;
            bool found = false;

            if (forge?.AvailableCommands == null || forge.BuildingSO?.TechTree == null)
            {
                return -1;
            }

            TechTreeSO techTree = forge.BuildingSO.TechTree;
            for (int i = 0; i < forge.AvailableCommands.Length; i++)
            {
                if (forge.AvailableCommands[i] is not ResearchUpgradeCommand research
                    || research.Upgrade == null
                    || !TryParseResearchTier(research.Upgrade, out int tier))
                {
                    continue;
                }

                UpgradeSO upgrade = research.Upgrade;
                if (!techTree.IsUnlocked(owner, upgrade))
                {
                    continue;
                }

                if (upgrade.IsOneTimeUnlock && techTree.IsResearched(owner, upgrade))
                {
                    continue;
                }

                if (forge.ContainsInQueue(upgrade))
                {
                    continue;
                }

                if (tier < lowest)
                {
                    lowest = tier;
                    found = true;
                }
            }

            return found ? lowest : -1;
        }

        private static bool ForgeQueueHasTierResearchInProgress(BaseBuilding forge, int tier)
        {
            if (forge == null || tier <= 0)
            {
                return false;
            }

            UnlockableSO[] queue = forge.Queue;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] is UpgradeSO upgrade
                    && TryParseResearchTier(upgrade, out int upgradeTier)
                    && upgradeTier == tier)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ForgeQueueHasAnyResearchInProgress(BaseBuilding forge)
        {
            if (forge == null || forge.QueueSize == 0)
            {
                return false;
            }

            UnlockableSO[] queue = forge.Queue;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] is UpgradeSO)
                {
                    return true;
                }
            }

            return false;
        }

        private static ForgeResearchPlanState GetOrCreateState(Owner owner)
        {
            if (!statesByOwner.TryGetValue(owner, out ForgeResearchPlanState state))
            {
                state = new ForgeResearchPlanState();
                statesByOwner[owner] = state;
            }

            return state;
        }
    }
}
