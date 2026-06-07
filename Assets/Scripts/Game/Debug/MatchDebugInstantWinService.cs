using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.InGame;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Game.DebugCheats
{
    /// <summary>
    /// SRP: Cheat debug — phá Civil Central địch để kích hoạt luồng chiến thắng.
    /// </summary>
    public static class MatchDebugInstantWinService
    {
        public const int InstantWinDamage = 9999;

        /// <summary>
        /// Mục tiêu: Gây sát thương lớn lên CC địch → MatchOutcomeDetector xử lý thắng/trận.
        /// Cách hoạt động: Resolve owner địch → tìm CC theo Owner (không fog) → TakeDamage.
        /// </summary>
        public static bool TryDestroyEnemyCivilCentral(out string failureReason)
        {
            failureReason = null;

            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                failureReason = "Không phải scene gameplay.";
                return false;
            }

            MatchOutcomeDetector.EnsureExists();

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            Owner enemyOwner = ResolveEnemyOwner(localOwner);

            if (!TryFindEnemyCivilCentral(localOwner, enemyOwner, out BaseBuilding enemyCc))
            {
                failureReason =
                    $"Không tìm thấy Civil Central địch (local={localOwner}, thử enemy={enemyOwner}).";
                return false;
            }

            int damage = Mathf.Max(InstantWinDamage, enemyCc.MaxHealth);
            enemyCc.TakeDamage(damage);
            return true;
        }

        static bool TryFindEnemyCivilCentral(Owner localOwner, Owner enemyOwner, out BaseBuilding civilCentral)
        {
            if (CivilCentralOwnerQuery.TryFindForOwner(enemyOwner, out civilCentral))
            {
                return true;
            }

            return CivilCentralOwnerQuery.TryFindFirstHostileCivilCentral(localOwner, out civilCentral);
        }

        static Owner ResolveEnemyOwner(Owner localOwner)
        {
            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            if (tracker != null)
            {
                Owner opponent = tracker.FindOpponentOwner(localOwner);
                if (opponent != localOwner && opponent != Owner.Invalid && opponent != Owner.Unowned)
                {
                    return opponent;
                }
            }

            if (localOwner == Owner.Player1)
            {
                return Owner.AI2;
            }

            if (localOwner == Owner.Player2)
            {
                return Owner.Player1;
            }

            return Owner.AI2;
        }
    }
}
