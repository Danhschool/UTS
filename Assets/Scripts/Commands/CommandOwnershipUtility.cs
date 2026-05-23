using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// SRP: Xác nhận unit/building nhận lệnh trùng phe với <see cref="CommandContext.Owner"/>.
    /// </summary>
    public static class CommandOwnershipUtility
    {
        /// <summary>
        /// Mục tiêu: Chặn AI/player gửi lệnh sang unit phe khác (kể cả khi bypass dispatcher).
        /// Cách hoạt động: So khớp <see cref="AbstractCommandable.Owner"/> với context.Owner.
        /// </summary>
        public static bool CommandableMatchesContextOwner(in CommandContext context)
        {
            return context.Commandable != null
                && context.Commandable.Owner != Owner.Invalid
                && context.Commandable.Owner != Owner.Unowned
                && context.Commandable.Owner == context.Owner;
        }
    }
}
