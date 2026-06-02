#if UNITY_EDITOR
using System.Collections.Generic;
using GameDevTV.RTS.Hotkeys;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// SRP: Tạo/cập nhật HotkeyBindingCatalogSO từ mapping mặc định.
    /// </summary>
    public static class HotkeyBindingCatalogSetupEditor
    {
        const string CatalogDirectory = "Assets/Data_Re/Hotkeys";
        const string CatalogPath = CatalogDirectory + "/HotkeyBindingCatalog.asset";

        [InitializeOnLoadMethod]
        static void EnsureCatalogOnEditorLoad()
        {
            // Chỉ đảm bảo có asset khi editor load; không ghi đè nội dung để tránh import loop/timestamp mismatch.
            EnsureCatalog(logIfCreated: false, overwriteExistingContent: false);
        }

        [MenuItem("ProjectRTS/Hotkeys/Create Binding Catalog")]
        public static void CreateOrUpdateBindingCatalog()
        {
            EnsureCatalog(logIfCreated: true, overwriteExistingContent: true);
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo project luôn có HotkeyBindingCatalogSO để UI/HotkeySystem dùng chung.
        /// Cách hoạt động: Tạo asset nếu thiếu và ghi danh sách từ HotkeyDefaults vào catalog.
        /// </summary>
        static void EnsureCatalog(bool logIfCreated, bool overwriteExistingContent)
        {
            EnsureDirectoryExists(CatalogDirectory);

            HotkeyBindingCatalogSO catalog = AssetDatabase.LoadAssetAtPath<HotkeyBindingCatalogSO>(CatalogPath);
            bool created = false;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HotkeyBindingCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                created = true;
            }

            bool shouldWriteContent = created || overwriteExistingContent;
            if (shouldWriteContent)
            {
                IReadOnlyList<HotkeyBindingEntry> defaults = HotkeyDefaults.CreateBindings();
                catalog.SetBindings(defaults);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }

            if (logIfCreated)
            {
                string state = created ? "Đã tạo mới" : (shouldWriteContent ? "Đã cập nhật" : "Đã kiểm tra");
                Debug.Log($"[Hotkeys] {state} Binding Catalog tại {CatalogPath}.", catalog);
            }
        }

        /// <summary>
        /// Mục tiêu: Tạo thư mục đích trước khi tạo asset catalog.
        /// Cách hoạt động: Tách path theo từng cấp và gọi AssetDatabase.CreateFolder nếu thư mục chưa tồn tại.
        /// </summary>
        static void EnsureDirectoryExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
