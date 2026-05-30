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

        /// <summary>
        /// Mục tiêu: Khoảng cách tâm hai slot spawn khi mỗi unit chiếm sphere bán kính r.
        /// Cách hoạt động: Trả về 2 × r để các sphere không chồng nhau.
        /// </summary>
        public static float ComputeCenterSpacing(float sphereRadius) =>
            Mathf.Max(0.01f, sphereRadius * 2f);

        /// <summary>
        /// Mục tiêu: Dàn unit quanh anchor trên rìa CC (offset/spacing theo hướng local của nhà).
        /// Cách hoạt động: Quay offset local bằng anchorRotation rồi cộng anchor + bước * index.
        /// </summary>
        public static Vector3 GetPositionAtEdge(
            Vector3 anchorPosition,
            Vector3 firstOffsetLocal,
            Vector3 spacingLocal,
            Quaternion anchorRotation,
            int unitIndex)
        {
            Vector3 firstOffset = anchorRotation * firstOffsetLocal;
            Vector3 spacing = anchorRotation * spacingLocal;
            return anchorPosition + firstOffset + spacing * unitIndex;
        }
    }
}
