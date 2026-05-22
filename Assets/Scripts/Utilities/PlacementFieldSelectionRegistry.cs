using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: AI — lưu địa chỉ đặt nhà đã chọn theo phe và tránh chọn trùng trong cùng ô field (bán kính tối thiểu).
    /// Người chơi dùng ghost + Restrictions trên <see cref="Player.PlayerInput"/>; không gọi registry này.
    /// </summary>
    public static class PlacementFieldSelectionRegistry
    {
        public const float SameFieldMinSeparation = 20f;

        private struct ReservedEntry
        {
            public Owner Owner;
            public Vector3 Position;
            public int CellX;
            public int CellZ;
        }

        private static readonly List<ReservedEntry> ReservedEntries = new();
        private static readonly Dictionary<Owner, Vector3> LastSelectedAddressByOwner = new();

        /// <summary>Địa chỉ đặt nhà được chọn gần nhất của phe (read-only).</summary>
        public static bool TryGetLastSelectedAddress(Owner owner, out Vector3 address) =>
            LastSelectedAddressByOwner.TryGetValue(owner, out address);

        /// <summary>
        /// Mục tiêu: Ô field đã có điểm đặt trong bán kính <paramref name="minSeparation"/> thì không dùng lại.
        /// Cách hoạt động: So cell (x,z); cùng ô và khoảng cách &lt; ngưỡng → từ chối.
        /// </summary>
        public static bool CanAcceptPlacement(
            Owner owner,
            Vector3 candidate,
            in PlacementFieldGridContext grid,
            float minSeparation = SameFieldMinSeparation)
        {
            float minSqr = minSeparation * minSeparation;
            bool hasCell = PlacementFieldGridUtility.TryGetCellIndices(candidate, grid, out int cx, out int cz);

            for (int i = 0; i < ReservedEntries.Count; i++)
            {
                ReservedEntry entry = ReservedEntries[i];
                if (entry.Owner != owner)
                {
                    continue;
                }

                if (hasCell && entry.CellX >= 0 && (entry.CellX != cx || entry.CellZ != cz))
                {
                    continue;
                }

                if ((entry.Position - candidate).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Mục tiêu: Ghi nhận địa chỉ vừa chọn khi bắt đầu đặt / enqueue build.
        /// Cách hoạt động: Cập nhật LastSelected + thêm vào danh sách reserved cùng ô field.
        /// </summary>
        public static void RegisterSelectedPlacement(
            Owner owner,
            Vector3 position,
            in PlacementFieldGridContext grid)
        {
            LastSelectedAddressByOwner[owner] = position;

            int cellX = -1;
            int cellZ = -1;
            if (PlacementFieldGridUtility.TryGetCellIndices(position, grid, out cellX, out cellZ))
            {
                // indices set
            }

            ReservedEntries.Add(new ReservedEntry
            {
                Owner = owner,
                Position = position,
                CellX = cellX,
                CellZ = cellZ
            });
        }

        /// <summary>
        /// Mục tiêu: Lưới field cho AI — influence nếu có, không thì quanh anchor.
        /// </summary>
        public static PlacementFieldGridContext ResolveGrid(
            Vector3 anchor,
            in AIInfluenceMapTickContext influence) =>
            influence.IsValid && influence.Map != null
                ? PlacementFieldGridContext.FromInfluenceMap(influence.Map)
                : PlacementFieldGridContext.FromWorldAnchor(anchor);

        public static void ClearReservationsForOwner(Owner owner)
        {
            LastSelectedAddressByOwner.Remove(owner);
            for (int i = ReservedEntries.Count - 1; i >= 0; i--)
            {
                if (ReservedEntries[i].Owner == owner)
                {
                    ReservedEntries.RemoveAt(i);
                }
            }
        }
    }
}
