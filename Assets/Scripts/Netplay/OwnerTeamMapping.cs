using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Ánh xạ slot/team Mirror sang <see cref="Owner"/> human của UTS.
    /// </summary>
    public static class OwnerTeamMapping
    {
        /// <summary>
        /// Mục tiêu: Biết owner có phải người chơi human (P1/P2) hay không.
        /// Cách hoạt động: So sánh với <see cref="Owner.Player1"/> và <see cref="Owner.Player2"/>.
        /// </summary>
        public static bool IsHumanPlayer(Owner owner) =>
            owner == Owner.Player1 || owner == Owner.Player2;

        /// <summary>
        /// Mục tiêu: Chuyển team lobby (0/1) sang owner UTS tương ứng.
        /// Cách hoạt động: Clamp index 0–1 → Player1 hoặc Player2; giá trị khác log cảnh báo và fallback Player1.
        /// </summary>
        public static Owner FromTeamIndex(int teamIndex)
        {
            int clamped = Mathf.Clamp(teamIndex, 0, 1);
            if (clamped != teamIndex)
                Debug.LogWarning($"[OwnerTeamMapping] teamIndex {teamIndex} ngoài 0–1; dùng {clamped}.");

            return clamped == 0 ? Owner.Player1 : Owner.Player2;
        }
    }
}
