using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Forge operational + gom lệnh research theo tier (dùng bởi <see cref="AIForgeResearchTierPlanner"/>).
    /// </summary>
    public static class AIForgeResearchRoundUtility
    {
        /// <summary>
        /// Mục tiêu: Forge sẵn sàng nhận lệnh research (đã xây xong hoặc legacy operational).
        /// Cách hoạt động: Quét snapshot.Buildings; khớp tên Forge + <see cref="IsForgeOperationalForResearch"/>.
        /// </summary>
        public static bool TryGetOperationalForge(
            AIWorldStateSnapshot snapshot,
            out BaseBuilding forge)
        {
            forge = null;
            if (snapshot?.Buildings == null)
            {
                return false;
            }

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding candidate = snapshot.Buildings[i];
                if (candidate?.BuildingSO == null
                    || candidate.BuildingSO.Name != AIInfraBuildUtility.ForgeDisplayName
                    || !IsForgeOperationalForResearch(candidate))
                {
                    continue;
                }

                forge = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Gom research tier cụ thể có thể enqueue (Available, đủ tiền) theo AvailableCommands.
        /// Cách hoạt động: Lọc <see cref="AIForgeResearchTierPlanner.TryParseResearchTier"/> == tier; tối đa maxCount.
        /// </summary>
        public static int CollectAffordableResearchCommandsForTier(
            BaseBuilding forge,
            Owner owner,
            int tier,
            List<ResearchUpgradeCommand> output,
            int maxCount)
        {
            output.Clear();
            if (forge?.AvailableCommands == null || maxCount <= 0 || tier <= 0)
            {
                return 0;
            }

            CommandContext context = new(owner, forge, AIHitUtility.AtPoint(forge.transform.position));
            for (int i = 0; i < forge.AvailableCommands.Length && output.Count < maxCount; i++)
            {
                if (forge.AvailableCommands[i] is not ResearchUpgradeCommand research
                    || research.Upgrade == null
                    || !AIForgeResearchTierPlanner.TryParseResearchTier(research.Upgrade, out int upgradeTier)
                    || upgradeTier != tier)
                {
                    continue;
                }

                if (research.IsLocked(context) || !research.IsAvailable(context))
                {
                    continue;
                }

                if (!SupplyAffordability.HasEnough(owner, research.Upgrade.Cost))
                {
                    continue;
                }

                output.Add(research);
            }

            return output.Count;
        }

        public static bool IsForgeOperationalForResearch(BaseBuilding candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.Progress.State == BuildingProgress.BuildingState.Completed)
            {
                return true;
            }

            if (candidate.Progress.State == BuildingProgress.BuildingState.Paused
                && candidate.Progress.Completion >= 0.99f)
            {
                return true;
            }

            return candidate.Progress.State == BuildingProgress.BuildingState.Building
                && candidate.MaxHealth > 0
                && candidate.CurrentHealth >= candidate.MaxHealth;
        }
    }
}
