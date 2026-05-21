using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Khóa mục tiêu gather hiện tại — BT tự loop; AI/player không gọi Gather trùng cùng node.
    /// </summary>
    internal sealed class WorkerGatherAssignmentLock
    {
        private int lockedSupplyGameObjectId;
        private GatherableSupply lockedSupply;

        public bool HasLock => lockedSupplyGameObjectId != 0;

        /// <summary>
        /// Mục tiêu: Có nên gọi GatherCommand/Gather tới node này không.
        /// Cách hoạt động: False nếu cùng gameObjectId đã khóa và node còn tài nguyên.
        /// </summary>
        public bool ShouldIssueGatherTo(GatherableSupply candidate)
        {
            RefreshStaleLock();
            if (candidate == null || candidate.Amount <= 0)
            {
                return false;
            }

            if (lockedSupplyGameObjectId == 0)
            {
                return true;
            }

            return lockedSupplyGameObjectId != candidate.gameObject.GetInstanceID();
        }

        /// <summary>
        /// Mục tiêu: Ghi nhận mỏ sau Gather thành công hoặc cập nhật khi đổi mỏ.
        /// Cách hoạt động: Lưu instance id + reference; Refresh trước khi so sánh.
        /// </summary>
        public void Assign(GatherableSupply supply)
        {
            if (supply == null)
            {
                Clear();
                return;
            }

            lockedSupplyGameObjectId = supply.gameObject.GetInstanceID();
            lockedSupply = supply;
        }

        /// <summary>
        /// Mục tiêu: Hủy khóa khi Stop/Build/Attack hoặc node không còn hợp lệ.
        /// Cách hoạt động: Reset id và reference về 0/null.
        /// </summary>
        public void Clear()
        {
            lockedSupplyGameObjectId = 0;
            lockedSupply = null;
        }

        /// <summary>
        /// Mục tiêu: Node bị phá/hết — không giữ khóa mỏ chết.
        /// Cách hoạt động: Nếu reference null hoặc Amount &lt;= 0 thì Clear.
        /// </summary>
        public void RefreshStaleLock()
        {
            if (lockedSupplyGameObjectId == 0)
            {
                return;
            }

            if (lockedSupply == null)
            {
                Clear();
                return;
            }

            if (lockedSupply.Amount <= 0)
            {
                Clear();
            }
        }
    }
}
