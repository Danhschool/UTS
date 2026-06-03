using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// SRP: Tầm nhìn khi thực thi lệnh — human dùng fog UI; AI dùng <see cref="FactionFogQuery"/> (sight radius phe bot).
    /// </summary>
    public static class CommandFactionVisibility
    {
        /// <summary>
        /// Mục tiêu: Collider hit có được commander nhìn thấy để Gather/Attack không.
        /// Cách hoạt động: Human → IHideable.IsVisible; AI → FactionFogQuery (không phụ thuộc fog Player1).
        /// </summary>
        public static bool IsHitVisibleToCommander(CommandContext context)
        {
            if (context.Hit.collider == null)
            {
                return false;
            }

            IHideable hideable = context.Hit.collider.GetComponentInParent<IHideable>();
            return hideable != null && IsVisibleToCommander(context.Owner, hideable);
        }

        /// <summary>
        /// Mục tiêu: API dùng chung cho Move tới ally hoặc kiểm tra entity.
        /// Cách hoạt động: Đồng minh luôn visible; human/AI phân nhánh như trên.
        /// </summary>
        public static bool IsVisibleToCommander(Owner commander, IHideable hideable)
        {
            if (hideable == null)
            {
                return false;
            }

            if (hideable is AbstractCommandable commandable && commandable.Owner == commander)
            {
                return true;
            }

            if (HumanFogVisionUtility.IsHumanPlayer(commander))
            {
                return hideable.IsVisible;
            }

            return FactionFogQuery.IsVisibleTo(commander, hideable);
        }
    }
}
