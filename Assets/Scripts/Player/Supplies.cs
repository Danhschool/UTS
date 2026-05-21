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

        private readonly HashSet<AbstractUnit> playerUnitsOnField = new(128);

        private void Awake()
        {
            Stone = new Dictionary<Owner, int>();
            Wood = new Dictionary<Owner, int>();
            Food = new Dictionary<Owner, int>();
            Population = new Dictionary<Owner, int>();
            PopulationLimit = new Dictionary<Owner, int>();

            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                Stone.Add(owner, 100);
                Wood.Add(owner, 100);
                Food.Add(owner, 100);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            RegisterExistingPlayerUnits();
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

            RefreshPlayer1PopulationHud();
        }

        private void RegisterExistingPlayerUnits()
        {
            playerUnitsOnField.Clear();
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit != null && unit.Owner == Owner.Player1 && unit.CurrentHealth > 0)
                {
                    playerUnitsOnField.Add(unit);
                }
            }
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (evt.Unit == null || evt.Unit.Owner != Owner.Player1)
            {
                return;
            }

            playerUnitsOnField.Add(evt.Unit);
            RefreshPlayer1PopulationHud();
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit == null)
            {
                return;
            }

            playerUnitsOnField.Remove(evt.Unit);
            RefreshPlayer1PopulationHud();
        }

        /// <summary>
        /// Mục tiêu: Cập nhật số unit Player1 đang sống trên Population Container (HUD).
        /// Cách hoạt động: Đếm HashSet, ghi vào Population và populationText giống stone/wood/food.
        /// </summary>
        private void RefreshPlayer1PopulationHud()
        {
            playerUnitsOnField.RemoveWhere(unit => unit == null || unit.CurrentHealth <= 0);

            int aliveCount = playerUnitsOnField.Count;
            Population[Owner.Player1] = aliveCount;

            if (populationText == null)
            {
                return;
            }

            int limit = PopulationLimit[Owner.Player1];
            populationText.SetText(limit > 0 ? $"{aliveCount}/{limit}" : aliveCount.ToString());
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
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null)
            {
                return;
            }

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
