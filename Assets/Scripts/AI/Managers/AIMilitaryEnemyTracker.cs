using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Vị trí Civil Central địch đã từng thấy (fair fog) — mục tiêu attack wave.
    /// </summary>
    public static class AIMilitaryEnemyTracker
    {
        private static readonly System.Collections.Generic.Dictionary<int, Vector3> LastKnownEnemyCcByKey =
            new(4);

        /// <summary>
        /// Mục tiêu: Cập nhật memory khi nhìn thấy nhà chính địch.
        /// Cách hoạt động: Key (aiOwner, enemyOwner); ghi transform.position.
        /// </summary>
        public static void RememberVisibleEnemyCivilCentral(Owner aiOwner, BaseBuilding enemyCc)
        {
            if (enemyCc == null || !enemyCc.IsVisible)
            {
                return;
            }

            int key = MakeKey(aiOwner, enemyCc.Owner);
            LastKnownEnemyCcByKey[key] = enemyCc.transform.position;
        }

        /// <summary>
        /// Mục tiêu: Điểm rally tấn công khi chưa thấy CC trực tiếp tick này.
        /// Cách hoạt động: Đọc cache; false nếu chưa scout bao giờ.
        /// </summary>
        public static bool TryGetLastKnownEnemyCcPosition(
            Owner aiOwner,
            Owner enemyOwner,
            out Vector3 position)
        {
            int key = MakeKey(aiOwner, enemyOwner);
            return LastKnownEnemyCcByKey.TryGetValue(key, out position);
        }

        public static void Clear(Owner aiOwner, Owner enemyOwner) =>
            LastKnownEnemyCcByKey.Remove(MakeKey(aiOwner, enemyOwner));

        private static int MakeKey(Owner aiOwner, Owner enemyOwner) =>
            ((int)aiOwner << 16) | (int)enemyOwner;
    }
}
