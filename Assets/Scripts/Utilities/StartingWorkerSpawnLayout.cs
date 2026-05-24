using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Tính vị trí spawn worker khởi đầu quanh điểm base (PvE / MP dùng chung).
    /// </summary>
    public static class StartingWorkerSpawnLayout
    {
        /// <summary>
        /// Mục tiêu: Tránh số âm khi chỉnh Inspector.
        /// Cách hoạt động: Clamp về &gt;= 0.
        /// </summary>
        public static int ClampCount(int count) => Mathf.Max(0, count);

        /// <summary>
        /// Mục tiêu: Đặt worker thứ i cạnh base, không chồng lên nhau.
        /// Cách hoạt động: base + offset đầu + spacing * index (thường dàn theo trục X).
        /// </summary>
        public static Vector3 GetPosition(
            Vector3 basePosition,
            Vector3 firstWorkerOffset,
            Vector3 spacingBetweenWorkers,
            int workerIndex) =>
            basePosition + firstWorkerOffset + spacingBetweenWorkers * workerIndex;
    }
}
