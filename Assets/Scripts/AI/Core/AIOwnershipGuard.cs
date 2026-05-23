using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Xác nhận entity thuộc phe AI đang chạy planner/dispatcher.
    /// </summary>
    public static class AIOwnershipGuard
    {
        /// <summary>
        /// Mục tiêu: Chặn AI điều khiển unit/building của phe khác.
        /// Cách hoạt động: So khớp <see cref="AbstractCommandable.Owner"/> với phe controller (không Invalid/Unowned).
        /// </summary>
        public static bool IsControlledBy(AbstractCommandable commandable, Owner factionOwner)
        {
            if (commandable == null || !IsPlayableFaction(factionOwner))
            {
                return false;
            }

            return commandable.Owner == factionOwner;
        }

        public static bool IsPlayableFaction(Owner owner) =>
            owner != Owner.Invalid && owner != Owner.Unowned;
    }
}
