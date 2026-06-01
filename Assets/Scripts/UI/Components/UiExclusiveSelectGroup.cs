using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Nhóm chọn một trong nhiều option (map list, độ khó AI…).
    /// Đặt trên parent; tự thu thập UiExclusiveSelectOption ở con nếu chưa gán.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiExclusiveSelectGroup : MonoBehaviour
    {
        [SerializeField] UiExclusiveSelectOption[] options;
        [SerializeField] int defaultIndex = 1;

        public event Action<int> SelectionChanged;

        public int SelectedIndex { get; private set; } = -1;

        void Awake()
        {
            RefreshOptions();
        }

        /// <summary>
        /// Mục tiêu: Thu thập lại option sau khi scene bootstrap thêm component runtime.
        /// Cách hoạt động: Quét con, bind index, áp dụng defaultIndex.
        /// </summary>
        public void RefreshOptions()
        {
            options = GetComponentsInChildren<UiExclusiveSelectOption>(true);
            SortOptionsByHierarchy(options);

            for (int i = 0; i < options.Length; i++)
            {
                options[i].Bind(this, i);
            }

            Select(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, options.Length - 1)), notify: false);
        }

        /// <summary>
        /// Mục tiêu: Chọn duy nhất một option và cập nhật Img Select.
        /// Cách hoạt động: Duyệt mảng options, bật visual cho index đích, tắt các index còn lại.
        /// </summary>
        public void Select(int index, bool notify = true)
        {
            if (options == null || options.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, options.Length - 1);

            if (SelectedIndex == index && notify)
            {
                return;
            }

            SelectedIndex = index;

            for (int i = 0; i < options.Length; i++)
            {
                options[i].ApplySelectedVisual(i == SelectedIndex);
            }

            if (notify)
            {
                SelectionChanged?.Invoke(SelectedIndex);
            }
        }

        static void SortOptionsByHierarchy(UiExclusiveSelectOption[] list)
        {
            if (list == null || list.Length < 2)
            {
                return;
            }

            List<UiExclusiveSelectOption> sorted = new(list);
            sorted.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            for (int i = 0; i < sorted.Count; i++)
            {
                list[i] = sorted[i];
            }
        }
    }
}
