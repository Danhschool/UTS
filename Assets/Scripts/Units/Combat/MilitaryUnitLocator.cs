using System.Collections.Generic;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Thu thập lính thuộc phe trong bán kính (rally / hội quân) — gọi khi phát lệnh, không trong Update.
    /// </summary>
    public static class MilitaryUnitLocator
    {
        /// <summary>
        /// Mục tiêu: Danh sách <see cref="BaseMilitaryUnit"/> còn sống trong bán kính quanh điểm.
        /// Cách hoạt động: Quét scene một lần; lọc Owner và khoảng cách ngang.
        /// </summary>
        public static void CollectMilitaryInRadius(
            Owner owner,
            Vector3 center,
            float radius,
            List<AbstractUnit> output)
        {
            output.Clear();
            if (radius <= 0f)
            {
                return;
            }

            float radiusSqr = radius * radius;
            BaseMilitaryUnit[] units = Object.FindObjectsByType<BaseMilitaryUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                BaseMilitaryUnit unit = units[i];
                if (unit == null
                    || unit.Owner != owner
                    || unit.CurrentHealth <= 0
                    || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 delta = unit.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                output.Add(unit);
            }
        }
    }
}
