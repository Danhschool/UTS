using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Lưu kết quả trận qua scene load (gameplay → End).
    /// </summary>
    public static class MatchOutcomeSessionState
    {
        public struct Payload
        {
            public MatchOutcomeResult Result;
            public Owner LocalOwner;
            public Owner OpponentOwner;
            public string LocalFactionLabel;
            public string OpponentFactionLabel;
            public FactionSummarySnapshot LocalSummary;
            public FactionSummarySnapshot OpponentSummary;
        }

        static bool hasPending;
        static Payload pending;

        public static bool HasPending => hasPending;

        /// <summary>
        /// Mục tiêu: Ghi nhận kết quả + snapshot thống kê trước khi load scene End.
        /// Cách hoạt động: Lưu vào static fields sống qua SceneManager.LoadScene; tracker có thể bị hủy sau đó.
        /// </summary>
        public static void Set(
            MatchOutcomeResult result,
            Owner localOwner,
            Owner opponentOwner,
            string localFactionLabel,
            string opponentFactionLabel,
            FactionSummarySnapshot localSummary,
            FactionSummarySnapshot opponentSummary)
        {
            hasPending = true;
            pending = new Payload
            {
                Result = result,
                LocalOwner = localOwner,
                OpponentOwner = opponentOwner,
                LocalFactionLabel = localFactionLabel,
                OpponentFactionLabel = opponentFactionLabel,
                LocalSummary = localSummary,
                OpponentSummary = opponentSummary
            };
        }

        /// <summary>
        /// Mục tiêu: End scene đọc dữ liệu một lần khi mở.
        /// Cách hoạt động: Trả payload và xóa cờ pending để tránh hiển thị lại khi reload.
        /// </summary>
        public static bool TryConsume(out Payload payload)
        {
            if (!hasPending)
            {
                payload = default;
                return false;
            }

            payload = pending;
            hasPending = false;
            return true;
        }
    }
}
