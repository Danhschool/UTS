using System.Text;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Kiểm tra đủ tài nguyên và đăng cảnh báo lên khung sự kiện (Player1).
    /// </summary>
    public static class SupplyAffordability
    {
        private const float WarningCooldownSeconds = 1.5f;

        private static string lastWarningMessage;
        private static float lastWarningTime = -999f;

        /// <summary>
        /// Mục tiêu: Xác định người chơi có đủ chi phí hay không.
        /// Cách hoạt động: So sánh Stone/Wood/Food hiện có với <see cref="SupplyCostSO"/>.
        /// </summary>
        public static bool HasEnough(Owner owner, SupplyCostSO cost)
        {
            if (cost == null)
            {
                return true;
            }

            return cost.Stone <= Supplies.Stone[owner]
                && cost.Wood <= Supplies.Wood[owner]
                && cost.Food <= Supplies.Food[owner];
        }

        /// <summary>
        /// Mục tiêu: Thông báo thiếu tài nguyên khi thao tác build/research thất bại.
        /// Cách hoạt động: Chỉ Player1; liệt kê từng loại thiếu; chống spam cùng một dòng trong 1.5s.
        /// </summary>
        public static void WarnPlayerIfInsufficient(Owner owner, SupplyCostSO cost, string actionDescription)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(owner)
                || !LocalHumanOwnerAccess.IsLocalOwner(owner)
                || cost == null
                || HasEnough(owner, cost))
            {
                return;
            }

            string message = BuildInsufficientMessage(owner, cost, actionDescription);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (message == lastWarningMessage && Time.time - lastWarningTime < WarningCooldownSeconds)
            {
                return;
            }

            lastWarningMessage = message;
            lastWarningTime = Time.time;
            GameEventLog.Post(message, GameEventLogCategory.Warning);
        }

        private static string BuildInsufficientMessage(Owner owner, SupplyCostSO cost, string actionDescription)
        {
            StringBuilder builder = new(128);
            builder.Append("Không đủ tài nguyên");

            if (!string.IsNullOrWhiteSpace(actionDescription))
            {
                builder.Append(" để ").Append(actionDescription.Trim());
            }

            builder.Append(':');

            bool anyShortfall = false;
            AppendShortfall(builder, ref anyShortfall, cost.Stone, Supplies.Stone[owner], "Đá");
            AppendShortfall(builder, ref anyShortfall, cost.Wood, Supplies.Wood[owner], "Gỗ");
            AppendShortfall(builder, ref anyShortfall, cost.Food, Supplies.Food[owner], "Lương thực");

            return anyShortfall ? builder.ToString() : null;
        }

        private static void AppendShortfall(
            StringBuilder builder,
            ref bool anyShortfall,
            int required,
            int available,
            string resourceLabel)
        {
            if (required <= 0 || available >= required)
            {
                return;
            }

            if (anyShortfall)
            {
                builder.Append(';');
            }
            else
            {
                builder.Append(' ');
            }

            builder.Append(" thiếu ")
                .Append(required - available)
                .Append(' ')
                .Append(resourceLabel)
                .Append(" (có ")
                .Append(available)
                .Append(')');

            anyShortfall = true;
        }
    }
}
