using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Map <see cref="SupplySO"/> → cue gather (stone / wood / food).
    /// </summary>
    public static class SupplyGatherAudioUtility
    {
        const string StoneSupplyPath = "Assets/Data_Re/Supply/SupplySO_Stone.asset";
        const string WoodSupplyPath = "Assets/Data_Re/Supply/SupplySO_Wood.asset";
        const string FoodSupplyPath = "Assets/Data_Re/Supply/SupplySO_Food.asset";

        static SupplySO stoneReference;
        static SupplySO woodReference;
        static SupplySO foodReference;

        /// <summary>
        /// Mục tiêu: Mỗi loại tài nguyên một âm khai thác riêng.
        /// Cách hoạt động: So reference tới SupplySO project; fallback theo tên asset.
        /// </summary>
        public static AudioCueId ResolveGatherCue(SupplySO supply)
        {
            if (supply == null)
            {
                return AudioCueId.None;
            }

            EnsureReferencesLoaded();

            if (ReferenceEquals(supply, stoneReference))
            {
                return AudioCueId.GatherStone;
            }

            if (ReferenceEquals(supply, woodReference))
            {
                return AudioCueId.GatherWood;
            }

            if (ReferenceEquals(supply, foodReference))
            {
                return AudioCueId.GatherFood;
            }

            string name = supply.name;
            if (name.Contains("Stone"))
            {
                return AudioCueId.GatherStone;
            }

            if (name.Contains("Wood"))
            {
                return AudioCueId.GatherWood;
            }

            if (name.Contains("Food") || name.Contains("Fruit") || name.Contains("Meat"))
            {
                return AudioCueId.GatherFood;
            }

            return AudioCueId.None;
        }

        static void EnsureReferencesLoaded()
        {
            if (stoneReference != null && woodReference != null && foodReference != null)
            {
                return;
            }

#if UNITY_EDITOR
            stoneReference ??= UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(StoneSupplyPath);
            woodReference ??= UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(WoodSupplyPath);
            foodReference ??= UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(FoodSupplyPath);
#endif
        }
    }
}
