using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: So khớp building với prefab archetype (hotkey A/S/D).
    /// </summary>
    public static class BuildingKindMatching
    {
        /// <summary>
        /// Mục tiêu: Phân biệt prefab cấu hình hotkey là nhà hay unit.
        /// Cách hoạt động: Kiểm tra <see cref="BaseBuilding"/> trên root hoặc con của prefab.
        /// </summary>
        public static bool IsBuildingPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            return prefab.GetComponent<BaseBuilding>() != null
                || prefab.GetComponentInChildren<BaseBuilding>(true) != null;
        }

        /// <summary>
        /// Mục tiêu: Kiểm tra instance nhà có cùng archetype với prefab hotkey.
        /// Cách hoạt động: So <see cref="BuildingSO.Prefab"/> với referencePrefab.
        /// </summary>
        public static bool MatchesPrefab(BaseBuilding building, GameObject referencePrefab)
        {
            if (building == null || referencePrefab == null)
            {
                return false;
            }

            BuildingSO buildingSo = building.BuildingSO;
            return buildingSo != null && buildingSo.Prefab == referencePrefab;
        }
    }
}
