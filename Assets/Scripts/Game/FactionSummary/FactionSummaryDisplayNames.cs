using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Game.FactionSummary
{
    /// <summary>
    /// SRP: Map tên SO/class unit sang nhãn tiếng Việt trên panel tóm tắt.
    /// </summary>
    public static class FactionSummaryDisplayNames
    {
        /// <summary>
        /// Mục tiêu: Hiển thị tên unit quen thuộc (Dân, Bộ binh, …) thay vì tên SO nội bộ.
        /// Cách hoạt động: Ưu tiên class cụ thể (Archer, Worker), sau đó map theo UnitSO.Name.
        /// </summary>
        public static string ResolveUnit(AbstractUnit unit)
        {
            if (unit == null)
            {
                return "Không rõ";
            }

            if (unit is Archer)
            {
                return "Cung thủ";
            }

            if (unit is Worker)
            {
                return "Dân";
            }

            if (unit.UnitSO != null && TryMapUnitSoName(unit.UnitSO.Name, out string mapped))
            {
                return mapped;
            }

            return unit.UnitSO != null && !string.IsNullOrWhiteSpace(unit.UnitSO.Name)
                ? unit.UnitSO.Name
                : unit.GetType().Name;
        }

        public static string ResolveBuilding(BaseBuilding building)
        {
            if (building?.UnitSO == null || string.IsNullOrWhiteSpace(building.UnitSO.Name))
            {
                return "Không rõ";
            }

            return building.UnitSO.Name;
        }

        static bool TryMapUnitSoName(string soName, out string display)
        {
            switch (soName)
            {
                case "Unit":
                case "Worker":
                    display = "Dân";
                    return true;
                case "Knight":
                    display = "Đá binh";
                    return true;
                case "Warrior":
                    display = "Bộ binh";
                    return true;
                case "Archer":
                    display = "Cung thủ";
                    return true;
                default:
                    display = null;
                    return false;
            }
        }
    }
}
