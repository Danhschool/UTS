using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: So khớu hai unit có cùng archetype (double-click chọn cùng loại, v.v.).
    /// </summary>
    public static class UnitKindMatching
    {
        /// <summary>
        /// Mục tiêu: Xác định hai unit có cùng loại gameplay hay không.
        /// Cách hoạt động: Ưu tiên so prefab gốc trên UnitSO; fallback runtime type + tên hiển thị SO.
        /// </summary>
        public static bool IsSameKind(AbstractUnit a, AbstractUnit b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            AbstractUnitSO soA = a.UnitSO;
            AbstractUnitSO soB = b.UnitSO;
            if (soA == null || soB == null)
            {
                return soA == soB;
            }

            GameObject prefabA = soA.Prefab;
            GameObject prefabB = soB.Prefab;
            if (prefabA != null && prefabB != null)
            {
                return prefabA == prefabB;
            }

            return a.GetType() == b.GetType() && soA.Name == soB.Name;
        }

        /// <summary>
        /// Mục tiêu: Kiểm tra unit có thuộc archetype prefab cấu hình hotkey hay không.
        /// Cách hoạt động: So <see cref="AbstractUnitSO.Prefab"/> với referencePrefab.
        /// </summary>
        public static bool MatchesPrefab(AbstractUnit unit, GameObject referencePrefab)
        {
            if (unit == null || referencePrefab == null)
            {
                return false;
            }

            AbstractUnitSO unitSo = unit.UnitSO;
            return unitSo != null && unitSo.Prefab == referencePrefab;
        }
    }
}
