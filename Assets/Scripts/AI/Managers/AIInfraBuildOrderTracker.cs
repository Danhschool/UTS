using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Nhớ loại nhà AI đã enqueue/dispatch build — tránh giao trùng trước khi prefab vào registry.
    /// </summary>
    public static class AIInfraBuildOrderTracker
    {
        private static readonly List<PendingOrder> orders = new(16);

        /// <summary>
        /// Mục tiêu: Planner biết loại nhà này đã được đưa vào hàng đợi lệnh build.
        /// Cách hoạt động: So khớp Owner + tên hiển thị BuildingSO (không phân biệt worker).
        /// </summary>
        public static bool HasOrderedBuild(Owner owner, string buildingDisplayName)
        {
            if (string.IsNullOrEmpty(buildingDisplayName))
            {
                return false;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                PendingOrder order = orders[i];
                if (order.Owner == owner && order.BuildingDisplayName == buildingDisplayName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Ghi nhận ngay khi enqueue BuildBuilding thành công.
        /// Cách hoạt động: Thay order cùng owner+ tên; lưu workerInstanceId để sync/gỡ sau.
        /// </summary>
        public static void RegisterOrder(Owner owner, string buildingDisplayName, int workerInstanceId)
        {
            if (string.IsNullOrEmpty(buildingDisplayName) || workerInstanceId == 0)
            {
                return;
            }

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                PendingOrder order = orders[i];
                if (order.Owner == owner && order.BuildingDisplayName == buildingDisplayName)
                {
                    orders.RemoveAt(i);
                }
            }

            orders.Add(new PendingOrder(owner, buildingDisplayName, workerInstanceId));
        }

        /// <summary>
        /// Mục tiêu: Gỡ khi dispatch bị chặn hoặc worker không còn nhận lệnh build loại đó.
        /// Cách hoạt động: Xóa mọi order trùng owner + buildingDisplayName.
        /// </summary>
        public static void ClearOrder(Owner owner, string buildingDisplayName)
        {
            if (string.IsNullOrEmpty(buildingDisplayName))
            {
                return;
            }

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                PendingOrder order = orders[i];
                if (order.Owner == owner && order.BuildingDisplayName == buildingDisplayName)
                {
                    orders.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Gỡ theo worker (hủy build / xây xong / đổi lệnh).
        /// Cách hoạt động: Xóa order có WorkerInstanceId trùng.
        /// </summary>
        public static void ClearOrdersForWorker(Owner owner, int workerInstanceId)
        {
            if (workerInstanceId == 0)
            {
                return;
            }

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                PendingOrder order = orders[i];
                if (order.Owner == owner && order.WorkerInstanceId == workerInstanceId)
                {
                    orders.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Dọn order “mồ côi” mỗi tick — nhà đã có trên map hoặc worker không còn cam kết build.
        /// Cách hoạt động: Duyệt orders; gỡ nếu building đã spawn hoặc worker không còn target đúng tên.
        /// </summary>
        public static void SyncWithWorld(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                PendingOrder order = orders[i];
                if (order.Owner != snapshot.Owner)
                {
                    continue;
                }

                if (HasBuildingOnMap(snapshot, order.BuildingDisplayName))
                {
                    orders.RemoveAt(i);
                    continue;
                }

                Worker worker = FindWorker(snapshot, order.WorkerInstanceId);
                if (worker == null)
                {
                    orders.RemoveAt(i);
                    continue;
                }

                if (!worker.TryGetCommittedBuildBuildingName(out string pendingName)
                    || pendingName != order.BuildingDisplayName)
                {
                    orders.RemoveAt(i);
                }
            }
        }

        private static bool HasBuildingOnMap(AIWorldStateSnapshot snapshot, string displayName)
        {
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO == null || building.BuildingSO.Name != displayName)
                {
                    continue;
                }

                if (building.Progress.State != BuildingProgress.BuildingState.Destroyed)
                {
                    return true;
                }
            }

            return false;
        }

        private static Worker FindWorker(AIWorldStateSnapshot snapshot, int workerInstanceId)
        {
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker != null && worker.GetInstanceID() == workerInstanceId)
                {
                    return worker;
                }
            }

            return null;
        }

        private readonly struct PendingOrder
        {
            public Owner Owner { get; }
            public string BuildingDisplayName { get; }
            public int WorkerInstanceId { get; }

            public PendingOrder(Owner owner, string buildingDisplayName, int workerInstanceId)
            {
                Owner = owner;
                BuildingDisplayName = buildingDisplayName;
                WorkerInstanceId = workerInstanceId;
            }
        }
    }
}
