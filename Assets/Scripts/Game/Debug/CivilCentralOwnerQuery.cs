using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Game.DebugCheats
{
    /// <summary>
    /// SRP: Tìm Civil Central theo Owner — không dùng fog / cache AI.
    /// </summary>
    public static class CivilCentralOwnerQuery
    {
        /// <summary>
        /// Mục tiêu: Lấy CC của một phe cụ thể (debug / cheat).
        /// Cách hoạt động: Quét mọi BaseBuilding active/inactive; khớp Owner + CivilCentralUtility.
        /// </summary>
        public static bool TryFindForOwner(Owner owner, out BaseBuilding civilCentral)
        {
            civilCentral = null;
            if (owner == Owner.Invalid || owner == Owner.Unowned)
            {
                return false;
            }

            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null || building.Owner != owner)
                {
                    continue;
                }

                if (!CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                civilCentral = building;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: CC đầu tiên không thuộc phe local (fallback khi ResolveEnemyOwner lệch).
        /// Cách hoạt động: Quét scene; bỏ qua Invalid/Unowned/localOwner.
        /// </summary>
        public static bool TryFindFirstHostileCivilCentral(Owner localOwner, out BaseBuilding civilCentral)
        {
            civilCentral = null;

            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null || !IsHostileOwner(building.Owner, localOwner))
                {
                    continue;
                }

                if (!CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                civilCentral = building;
                return true;
            }

            return false;
        }

        static bool IsHostileOwner(Owner candidate, Owner localOwner) =>
            candidate != localOwner
            && candidate != Owner.Invalid
            && candidate != Owner.Unowned;
    }
}
