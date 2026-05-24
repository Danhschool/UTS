using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Kế hoạch planner theo tick — bật/tắt economy/base/military/influence để giảm spike CPU.
    /// </summary>
    public readonly struct AIPlannerTickPlan
    {
        public readonly bool RunEconomy;
        public readonly bool RunBase;
        public readonly bool RunMilitary;
        public readonly bool RebuildInfluence;
        public readonly bool ResolveConfigs;
        public readonly bool RefreshSight;
        public readonly bool RecoverStaleGather;
        public readonly bool UseCoarseInfluenceGrid;

        public static AIPlannerTickPlan Full => new(
            runEconomy: true,
            runBase: true,
            runMilitary: true,
            rebuildInfluence: true,
            resolveConfigs: true,
            refreshSight: true,
            recoverStaleGather: true,
            useCoarseInfluenceGrid: false);

        public AIPlannerTickPlan(
            bool runEconomy,
            bool runBase,
            bool runMilitary,
            bool rebuildInfluence,
            bool resolveConfigs,
            bool refreshSight,
            bool recoverStaleGather,
            bool useCoarseInfluenceGrid)
        {
            RunEconomy = runEconomy;
            RunBase = runBase;
            RunMilitary = runMilitary;
            RebuildInfluence = rebuildInfluence;
            ResolveConfigs = resolveConfigs;
            RefreshSight = refreshSight;
            RecoverStaleGather = recoverStaleGather;
            UseCoarseInfluenceGrid = useCoarseInfluenceGrid;
        }

        public bool NeedsSceneEntityCache => RunMilitary || RunEconomy;

        /// <summary>
        /// Mục tiêu: Chia nhỏ công việc nặng giữa các tick (chấp nhận phản ứng chậm hơn).
        /// Cách hoạt động: Modulo plannerTickIndex với interval từng subsystem; influence/config thưa hơn.
        /// </summary>
        public static AIPlannerTickPlan Build(
            int plannerTickIndex,
            bool performanceMode,
            int economyEvery,
            int baseEvery,
            int militaryEvery,
            int influenceEvery,
            int configEvery,
            int sightEvery,
            int staleGatherEvery)
        {
            if (!performanceMode)
            {
                return Full;
            }

            int tick = Mathf.Max(1, plannerTickIndex);
            return new AIPlannerTickPlan(
                runEconomy: tick % Mathf.Max(1, economyEvery) == 0,
                runBase: tick % Mathf.Max(1, baseEvery) == 0,
                runMilitary: tick % Mathf.Max(1, militaryEvery) == 0,
                rebuildInfluence: tick % Mathf.Max(1, influenceEvery) == 0,
                resolveConfigs: tick % Mathf.Max(1, configEvery) == 0,
                refreshSight: tick % Mathf.Max(1, sightEvery) == 0,
                recoverStaleGather: tick % Mathf.Max(1, staleGatherEvery) == 0,
                useCoarseInfluenceGrid: true);
        }
    }
}
