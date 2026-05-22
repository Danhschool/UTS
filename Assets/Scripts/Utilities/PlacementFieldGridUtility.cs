using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Ánh xạ tọa độ thế giới → chỉ số ô trên lưới placement field.
    /// </summary>
    public static class PlacementFieldGridUtility
    {
        /// <summary>
        /// Mục tiêu: Xác định ô (field) chứa điểm đặt nhà.
        /// Cách hoạt động: Floor (world − origin) / cellSize; kiểm tra trong biên grid.
        /// </summary>
        public static bool TryGetCellIndices(
            Vector3 world,
            in PlacementFieldGridContext grid,
            out int cellX,
            out int cellZ)
        {
            cellX = 0;
            cellZ = 0;
            if (!grid.IsValid)
            {
                return false;
            }

            cellX = Mathf.FloorToInt((world.x - grid.Origin.x) / grid.CellSize);
            cellZ = Mathf.FloorToInt((world.z - grid.Origin.z) / grid.CellSize);
            return cellX >= 0 && cellX < grid.GridWidth && cellZ >= 0 && cellZ < grid.GridWidth;
        }
    }
}
