using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Tham số lưới ô (field) dùng để so khớp điểm đặt nhà — influence map hoặc lưới quanh anchor.
    /// </summary>
    public readonly struct PlacementFieldGridContext
    {
        public float CellSize { get; }
        public Vector3 Origin { get; }
        public int GridWidth { get; }
        public bool IsValid { get; }

        public PlacementFieldGridContext(float cellSize, Vector3 origin, int gridWidth, bool isValid)
        {
            CellSize = cellSize;
            Origin = origin;
            GridWidth = gridWidth;
            IsValid = isValid && cellSize > 0f && gridWidth > 0;
        }

        /// <summary>
        /// Mục tiêu: Lưới field trùng với <see cref="GameDevTV.RTS.AI.AIInfluenceMap"/> của tick.
        /// Cách hoạt động: Đọc CellSize, Origin, GridWidth từ map đã rebuild.
        /// </summary>
        public static PlacementFieldGridContext FromInfluenceMap(GameDevTV.RTS.AI.AIInfluenceMap map) =>
            map != null && map.GridWidth > 0
                ? new PlacementFieldGridContext(map.CellSize, map.Origin, map.GridWidth, true)
                : default;

        /// <summary>
        /// Mục tiêu: Lưới fallback khi AI không có influence map trong tick.
        /// Cách hoạt động: Căn origin quanh anchor với bán kính mapRadius và cellSize cố định.
        /// </summary>
        public static PlacementFieldGridContext FromWorldAnchor(
            Vector3 anchor,
            float cellSize = 12f,
            float mapRadius = 96f)
        {
            int half = Mathf.Max(1, Mathf.CeilToInt(mapRadius / cellSize));
            int width = half * 2 + 1;
            Vector3 origin = anchor - new Vector3(half * cellSize, 0f, half * cellSize);
            return new PlacementFieldGridContext(cellSize, origin, width, true);
        }
    }
}
