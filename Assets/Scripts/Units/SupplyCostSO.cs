using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Supply Cost", menuName = "Supply Cost", order = 5)]
    public class SupplyCostSO : ScriptableObject
    {
        [field: SerializeField, FormerlySerializedAs("Minerals")]
        public int Stone { get; private set; } = 50;

        [field: SerializeField, FormerlySerializedAs("MineralsSO")]
        public SupplySO StoneSO { get; private set; }

        [field: SerializeField, FormerlySerializedAs("Gas")]
        public int Wood { get; private set; } = 0;

        [field: SerializeField, FormerlySerializedAs("GasSO")]
        public SupplySO WoodSO { get; private set; }

        [field: SerializeField]
        public int Food { get; private set; } = 0;

        [field: SerializeField]
        public SupplySO FoodSO { get; private set; }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (StoneSO == null)
            {
                StoneSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Stone.asset");
            }

            if (WoodSO == null)
            {
                WoodSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Wood.asset");
            }

            if (FoodSO == null)
            {
                FoodSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Food.asset");
            }
        }
#endif
    }
}
