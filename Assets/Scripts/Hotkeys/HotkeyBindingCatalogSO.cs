using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Dữ liệu cấu hình mapping phím → HotkeyId. Tạo asset: Create > RTS > Hotkeys > Binding Catalog.
    /// </summary>
    [CreateAssetMenu(fileName = "HotkeyBindingCatalog", menuName = "RTS/Hotkeys/Binding Catalog", order = 0)]
    public sealed class HotkeyBindingCatalogSO : ScriptableObject
    {
        [SerializeField] List<HotkeyBindingEntry> bindings = new();

        public IReadOnlyList<HotkeyBindingEntry> Bindings => bindings;

        public void SetBindings(IReadOnlyList<HotkeyBindingEntry> source)
        {
            bindings.Clear();
            if (source == null)
            {
                return;
            }

            bindings.AddRange(source);
        }
    }
}
