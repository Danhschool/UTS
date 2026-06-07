using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Suy ra thắng/thua theo góc nhìn phe local khi trận kết thúc.
    /// </summary>
    public static class MatchOutcomeResolver
    {
        /// <summary>
        /// Mục tiêu: Xác định local thắng hay thua khi một Civil Central bị phá.
        /// Cách hoạt động: CC của local → thua; CC phe khác → thắng.
        /// </summary>
        public static MatchOutcomeResult ResolveFromCivilCentralDestroyed(Owner destroyedOwner, Owner localOwner)
        {
            return destroyedOwner == localOwner
                ? MatchOutcomeResult.Defeat
                : MatchOutcomeResult.Victory;
        }

        public static string GetTitle(MatchOutcomeResult result)
        {
            return result == MatchOutcomeResult.Victory
                ? "CHIẾN THẮNG"
                : "THUA CUỘC";
        }

        public static string GetFactionLabel(Owner owner, Owner localOwner)
        {
            return owner == localOwner ? "Bạn" : "Đối phương";
        }
    }
}
