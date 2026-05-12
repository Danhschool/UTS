using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Player
{
    public class Supplies : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("mineralsText")]
        private TextMeshProUGUI stoneText;

        [SerializeField, FormerlySerializedAs("gasText")]
        private TextMeshProUGUI woodText;

        [SerializeField]
        private TextMeshProUGUI foodText;

        [SerializeField] private TextMeshProUGUI populationText;

        [SerializeField, FormerlySerializedAs("mineralsSO")]
        private SupplySO stoneSO;

        [SerializeField, FormerlySerializedAs("gasSO")]
        private SupplySO woodSO;

        [SerializeField]
        private SupplySO foodSO;

        public static Dictionary<Owner, int> Stone { get; private set; }
        public static Dictionary<Owner, int> Wood { get; private set; }
        public static Dictionary<Owner, int> Food { get; private set; }
        public static Dictionary<Owner, int> Population { get; private set; }
        public static Dictionary<Owner, int> PopulationLimit { get; private set; }

        private void Awake()
        {
            Stone = new Dictionary<Owner, int>();
            Wood = new Dictionary<Owner, int>();
            Food = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Stone.Add(owner, 0);
                Wood.Add(owner, 0);
                Food.Add(owner, 0);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            RefreshPlayer1SupplyHud();
        }

        /// <summary>
        /// Đồng bộ HUD tài nguyên của Player1 với số liệu runtime (mặc định 0 khi mới vào game).
        /// </summary>
        /// <remarks>
        /// Trước đây chỉ cập nhật khi có <see cref="SupplyEvent"/> nên text trong scene không đổi cho đến lần thu/chi đầu tiên.
        /// </remarks>
        private void RefreshPlayer1SupplyHud()
        {
            if (stoneText != null)
            {
                stoneText.SetText(Stone[Owner.Player1].ToString());
            }

            if (woodText != null)
            {
                woodText.SetText(Wood[Owner.Player1].ToString());
            }

            if (foodText != null)
            {
                foodText.SetText(Food[Owner.Player1].ToString());
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (stoneSO == null)
            {
                stoneSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Stone.asset");
            }

            if (woodSO == null)
            {
                woodSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Wood.asset");
            }

            if (foodSO == null)
            {
                foodSO = UnityEditor.AssetDatabase.LoadAssetAtPath<SupplySO>(
                    "Assets/Data_Re/Supply/SupplySO_Food.asset");
            }
        }
#endif

        private void OnDestroy()
        {
            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply.Equals(stoneSO))
            {
                Stone[evt.Owner] += evt.Amount;
                if (Owner.Player1 == evt.Owner && stoneText != null)
                {
                    stoneText.SetText(Stone[evt.Owner].ToString());
                }
            }
            else if (evt.Supply.Equals(woodSO))
            {
                Wood[evt.Owner] += evt.Amount;
                if (Owner.Player1 == evt.Owner && woodText != null)
                {
                    woodText.SetText(Wood[evt.Owner].ToString());
                }
            }
            else if (evt.Supply.Equals(foodSO))
            {
                Food[evt.Owner] += evt.Amount;
                if (Owner.Player1 == evt.Owner && foodText != null)
                {
                    foodText.SetText(Food[evt.Owner].ToString());
                }
            }
        }
    }
}
