using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Ngưỡng spawn quân trước mỗi tháp vòng — tổng đã spawn (kể cả chết), bậc × (số tháp + 1).
    /// </summary>
    public static class AIMilitaryDefenseRingTowerArmyGate
    {
        /// <summary>
        /// Mục tiêu: Tháp tiếp theo cần tier×(N+1) lính đã từng spawn (vd 10→tháp 1, 20→tháp 2).
        /// </summary>
        public static int GetRequiredLifetimeMilitarySpawned(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings)
        {
            if (snapshot == null || settings == null)
            {
                return 0;
            }

            int tier = settings.MilitarySpawnedPerTowerTier;
            int towerCount = AIInfraBuildUtility.CountInfraBuildings(
                snapshot,
                AIInfraBuildUtility.DefenseTowerDisplayName);
            return tier * (towerCount + 1);
        }

        /// <summary>
        /// Mục tiêu: Đủ lính đã spawn (kể cả chết) hoặc đủ quân sống — tránh lệch bộ đếm registry.
        /// </summary>
        public static int GetEffectiveMilitaryCountForTowerGate(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            int alive = AIMilitaryOperationalLeash.CountAliveMilitary(snapshot);
            return Mathf.Max(snapshot.LifetimeMilitarySpawnCount, alive);
        }

        /// <summary>
        /// Mục tiêu: Đủ quân để queue tháp kế tiếp trên vòng.
        /// </summary>
        public static bool CanPlaceNextDefenseTower(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings) =>
            snapshot != null
            && settings != null
            && GetEffectiveMilitaryCountForTowerGate(snapshot)
                >= GetRequiredLifetimeMilitarySpawned(snapshot, settings);

        /// <summary>
        /// Mục tiêu: Đủ quân cho tháp tiếp theo → chưa train Barrack, ưu tiên xây tháp trước.
        /// </summary>
        public static bool ShouldDeferBarrackTrainingForTower(
            AIWorldStateSnapshot snapshot,
            AIMilitarySettings settings,
            bool defenseRingMode)
        {
            if (snapshot == null
                || settings == null
                || !CanPlaceNextDefenseTower(snapshot, settings)
                || AIInfraBuildUtility.HasPendingInfraBuild(
                    snapshot,
                    AIInfraBuildUtility.DefenseTowerDisplayName)
                || AIMilitaryExpansionPlanner.CountOperationalBarracks(snapshot) < 1)
            {
                return false;
            }

            if (defenseRingMode && settings.EnableDefenseRingExpansion)
            {
                return !AIMilitaryDefenseRingPlanner.HasReachedMaxRings(settings, snapshot.Owner);
            }

            int towers = AIInfraBuildUtility.CountInfraBuildings(
                snapshot,
                AIInfraBuildUtility.DefenseTowerDisplayName);
            return towers < Mathf.Max(1, settings.EarlyGameTargetDefenseTowerCount);
        }
    }
}
