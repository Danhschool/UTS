using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chặn AI economy chọn mỏ/supply trong vùng nhà human (PvAI M5).
    /// </summary>
    public static class AIEconomyGatherTerritoryGuard
    {
        public const float DefaultEnemyHomeExclusionRadius = 48f;

        /// <summary>
        /// Mục tiêu: Không gather tại mỏ quá gần Civil Central địch hoặc gần địch hơn nhà mình.
        /// Cách hoạt động: Tìm CC địch; loại nếu trong bán kính exclusion hoặc dist(enemy) &lt; dist(own); cap bán kính từ CC mình.
        /// </summary>
        public static bool IsGatherSupplyAllowed(
            AIWorldStateSnapshot snapshot,
            GatherableSupply supply,
            float enemyHomeExclusionRadius = DefaultEnemyHomeExclusionRadius)
        {
            if (snapshot == null || supply == null || snapshot.CivilCentral == null)
            {
                return true;
            }

            if (OwnerTeamMapping.IsHumanPlayer(snapshot.Owner))
            {
                return true;
            }

            Vector3 supplyPos = supply.transform.position;
            Vector3 ownCcPos = snapshot.CivilCentral.transform.position;
            float distToOwnSq = (supplyPos - ownCcPos).sqrMagnitude;

            Owner enemyOwner = ResolvePrimaryEnemyOwner(snapshot.Owner);
            if (!OwnerTeamMapping.IsHumanPlayer(enemyOwner))
            {
                return true;
            }

            if (!AIMilitaryHostileScanner.TryFindEnemyCivilCentral(enemyOwner, out BaseBuilding enemyCc)
                || enemyCc == null)
            {
                return true;
            }

            Vector3 enemyCcPos = enemyCc.transform.position;
            float exclusionSq = enemyHomeExclusionRadius * enemyHomeExclusionRadius;
            float distToEnemySq = (supplyPos - enemyCcPos).sqrMagnitude;
            if (distToEnemySq < exclusionSq)
            {
                return false;
            }

            return distToEnemySq >= distToOwnSq;
        }

        /// <summary>
        /// Mục tiêu: PvAI — phe AI mặc định coi Player1 là human địch.
        /// Cách hoạt động: Human AI owner → AI2; ngược lại → Player1.
        /// </summary>
        public static Owner ResolvePrimaryEnemyOwner(Owner aiOwner)
        {
            if (aiOwner == Owner.Player1)
            {
                return Owner.AI2;
            }

            if (OwnerTeamMapping.IsHumanPlayer(aiOwner))
            {
                return Owner.AI2;
            }

            return Owner.Player1;
        }
    }
}
