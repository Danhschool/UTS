using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Phase XP đầu (hạ tầng quân) và phase mở rộng (train + tấn công).
    /// </summary>
    public static class AIMilitaryExpansionPlanner
    {
        /// <summary>
        /// Mục tiêu: Đã đủ quân để chuyển trọng tâm sang train + scout/tấn công tổng.
        /// Cách hoạt động: So sánh MilitaryUnits.Count với ngưỡng trên AIMilitarySettings.
        /// </summary>
        public static bool IsExpansionPhase(AIWorldStateSnapshot snapshot, AIMilitarySettings settings)
        {
            if (snapshot == null || settings == null)
            {
                return false;
            }

            return snapshot.MilitaryUnits.Count >= settings.MilitaryExpansionArmyThreshold;
        }

        /// <summary>
        /// Mục tiêu: XP đầu — đủ Barrack + Tower mục tiêu (ưu tiên xây thêm).
        /// Cách hoạt động: Đếm nhà hoàn thành/đang xây; so với EarlyGameTarget* trên settings.
        /// </summary>
        public static bool HasMetEarlyMilitaryInfraTargets(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings)
        {
            if (snapshot == null || settings == null)
            {
                return false;
            }

            int barracks = AIInfraBuildUtility.CountInfraBuildings(snapshot, AIInfraBuildUtility.BarrackDisplayName);
            int towers = AIInfraBuildUtility.CountInfraBuildings(snapshot, AIInfraBuildUtility.DefenseTowerDisplayName);
            return barracks >= settings.EarlyGameTargetBarrackCount
                && towers >= settings.EarlyGameTargetDefenseTowerCount;
        }

        /// <summary>
        /// Mục tiêu: Đủ tài nguyên dự trữ trước train lính tại Barrack (không chặn xây Barrack/Tháp).
        /// Cách hoạt động: So snapshot Stone/Wood với Min*BeforeMilitaryExpansion trên settings.
        /// </summary>
        public static bool HasMilitaryExpansionResourceReserve(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings)
        {
            if (snapshot == null || settings == null)
            {
                return false;
            }

            return snapshot.Stone >= settings.MinStoneBeforeMilitaryExpansion
                && snapshot.Wood >= settings.MinWoodBeforeMilitaryExpansion;
        }

        /// <summary>
        /// Mục tiêu: Backbone xong nhưng chưa đủ Barrack/Tháp — ưu tiên xây, không train quân.
        /// </summary>
        public static bool IsMilitaryInfraBootstrapPhase(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings) =>
            snapshot != null
            && settings != null
            && AIInfraBuildUtility.HasBackboneInfraComplete(snapshot)
            && !HasMetEarlyMilitaryInfraTargets(snapshot, settings);

        /// <summary>
        /// Mục tiêu: Ưu tiên queue Barrack/Tower thay vì Store/Corral (sau backbone).
        /// </summary>
        public static bool ShouldPrioritizeMilitaryInfra(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings baseSettings,
            AIMilitarySettings militarySettings)
        {
            if (snapshot == null || militarySettings == null)
            {
                return false;
            }

            if (!AIInfraBuildUtility.HasBackboneInfraComplete(snapshot))
            {
                return false;
            }

            if (IsMilitaryInfraBootstrapPhase(snapshot, militarySettings))
            {
                return true;
            }

            return IsExpansionPhase(snapshot, militarySettings);
        }

        /// <summary>
        /// Mục tiêu: Train khi có Barrack xong và đủ dự trữ S/W (sau phase xây mở rộng).
        /// Cách hoạt động: IsOperationalBarrack + HasMilitaryExpansionResourceReserve.
        /// </summary>
        public static bool ShouldAllowBarrackTraining(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings) =>
            snapshot != null
            && settings != null
            && HasMilitaryExpansionResourceReserve(snapshot, settings)
            && CountOperationalBarracks(snapshot) >= 1;

        /// <summary>
        /// Mục tiêu: Đếm Barrack hoàn thành có thể train — khác đếm nhà đang xây.
        /// </summary>
        public static int CountOperationalBarracks(AIWorldStateSnapshot snapshot)
        {
            if (snapshot?.Buildings == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                if (AIMilitaryConfigResolver.IsOperationalBarrack(snapshot.Buildings[i]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Mục tiêu: Gom tối đa N lính rảnh (patrol-eligible) thành một squad tấn công.
        /// Cách hoạt động: Duyệt military list; bỏ unit đã assign tick; dừng khi đủ squad size.
        /// </summary>
        public static int CollectExpansionAttackSquad(
            IReadOnlyList<AbstractUnit> militaryUnits,
            HashSet<int> excludeUnitIds,
            int maxSquadSize,
            List<AbstractUnit> squadOut)
        {
            squadOut.Clear();
            if (militaryUnits == null || maxSquadSize <= 0)
            {
                return 0;
            }

            for (int i = 0; i < militaryUnits.Count && squadOut.Count < maxSquadSize; i++)
            {
                AbstractUnit unit = militaryUnits[i];
                if (unit == null
                    || unit.CurrentHealth <= 0
                    || excludeUnitIds != null && excludeUnitIds.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (!AIMilitaryPatrolUtility.IsEligibleForPatrol(unit))
                {
                    continue;
                }

                squadOut.Add(unit);
            }

            return squadOut.Count;
        }

        /// <summary>
        /// Mục tiêu: Vẫn cần ít worker khi expansion — không dừng hẳn economy.
        /// </summary>
        public static bool ShouldThrottleWorkerTraining(
            AIWorldStateSnapshot snapshot,
            AIBaseSettings baseSettings,
            AIMilitarySettings militarySettings)
        {
            if (!IsExpansionPhase(snapshot, militarySettings) || baseSettings == null)
            {
                return false;
            }

            return snapshot.Workers.Count >= baseSettings.MinWorkersDuringMilitaryExpansion;
        }
    }
}
