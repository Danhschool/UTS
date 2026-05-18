using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Panel giữa màn hình khi chọn từ 2 unit trở lên: icon theo loại + số lượng.
    /// </summary>
    public class MultiUnitSelectionUI : MonoBehaviour, IUIElement<IEnumerable<AbstractCommandable>>
    {
        [SerializeField] private MultiUnitTypeSlotUI[] typeSlots;

        private void Awake()
        {
            if (typeSlots == null || typeSlots.Length == 0)
            {
                typeSlots = GetComponentsInChildren<MultiUnitTypeSlotUI>(true);
            }
        }

        /// <summary>
        /// Mục tiêu: Hiện danh sách loại unit đang chọn (gom theo UnitSO.Name).
        /// Cách hoạt động: Nhóm selection, sắp xếp theo số lượng giảm dần, gán từng slot; slot thừa thì ẩn.
        /// </summary>
        public void EnableFor(IEnumerable<AbstractCommandable> selectedUnits)
        {
            gameObject.SetActive(true);

            List<UnitTypeGroup> groups = BuildGroups(selectedUnits);
            for (int i = 0; i < typeSlots.Length; i++)
            {
                if (i < groups.Count)
                {
                    UnitTypeGroup group = groups[i];
                    typeSlots[i].Set(group.Icon, group.Count);
                }
                else
                {
                    typeSlots[i].Hide();
                }
            }
        }

        public void Disable()
        {
            gameObject.SetActive(false);
            foreach (MultiUnitTypeSlotUI slot in typeSlots)
            {
                slot.Hide();
            }
        }

        private static List<UnitTypeGroup> BuildGroups(IEnumerable<AbstractCommandable> selectedUnits)
        {
            return selectedUnits
                .Where(unit => unit != null && unit.UnitSO != null)
                .GroupBy(unit => unit.UnitSO.Name)
                .Select(group => new UnitTypeGroup(group.Key, group.First().UnitSO.Icon, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.DisplayName)
                .ToList();
        }

        private readonly struct UnitTypeGroup
        {
            public UnitTypeGroup(string displayName, Sprite icon, int count)
            {
                DisplayName = displayName;
                Icon = icon;
                Count = count;
            }

            public string DisplayName { get; }
            public Sprite Icon { get; }
            public int Count { get; }
        }
    }
}
