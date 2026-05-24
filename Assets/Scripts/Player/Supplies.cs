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

        private Owner hudOwner = Owner.Player1;

        private static Supplies activeInstance;
        static bool busHandlersRegistered;

        /// <summary>
        /// Mục tiêu: Đồng bộ HUD dân sau khi unit Player1 spawn xong (Start / NotifySpawned).
        /// </summary>
        public static void RegisterPlayerUnit(AbstractUnit unit)
        {
            activeInstance?.TrackPlayerUnitSpawn(unit);
        }

        /// <summary>
        /// Mục tiêu: Tránh NullRef khi PlayerViewBinder/FactionHudBinder gọi trước Supplies.Awake (UI chưa active).
        /// Cách hoạt động: Tạo dictionary static một lần với giá trị mặc định cho mọi Owner.
        /// </summary>
        static void EnsureDictionariesInitialized()
        {
            if (Stone != null)
            {
                return;
            }

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
        }

        private void Awake()
        {
            activeInstance = this;
            EnsureDictionariesInitialized();
            RegisterBusHandlersIfNeeded();
            hudOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            RegisterExistingHudOwnerUnits();
            RefreshSupplyHud();
        }

        void OnEnable()
        {
            EnsureDictionariesInitialized();
            RegisterBusHandlersIfNeeded();
            RegisterExistingHudOwnerUnits();
            RefreshSupplyHud();
        }

        static void RegisterBusHandlersIfNeeded()
        {
            if (busHandlersRegistered || activeInstance == null)
            {
                return;
            }

            Bus<SupplyEvent>.RegisterForAll(activeInstance.HandleSupplyEvent);
            Bus<UnitSpawnEvent>.RegisterForAll(activeInstance.HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(activeInstance.HandleUnitDeath);
            busHandlersRegistered = true;
        }

        /// <summary>
        /// Mục tiêu: HUD S/W/F/Population theo human local (P1 offline, P2 client MP).
        /// Cách hoạt động: Gán hudOwner, quét lại unit phe đó, refresh text.
        /// </summary>
        public void BindHudOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            EnsureDictionariesInitialized();
            hudOwner = owner;

            if (!isActiveAndEnabled)
            {
                return;
            }

            RegisterExistingHudOwnerUnits();
            RefreshSupplyHud();
        }

        void RefreshSupplyHud()
        {
            EnsureDictionariesInitialized();
            if (Stone == null || !Stone.ContainsKey(hudOwner))
            {
                return;
            }

            if (stoneText != null)
            {
                stoneText.SetText(Stone[hudOwner].ToString());
            }

            if (woodText != null)
            {
                woodText.SetText(Wood[hudOwner].ToString());
            }

            if (foodText != null)
            {
                foodText.SetText(Food[hudOwner].ToString());
            }

            RefreshPopulationHud();
        }

        void RegisterExistingHudOwnerUnits()
        {
            playerUnitsOnField.Clear();
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit != null && unit.Owner == hudOwner && unit.CurrentHealth > 0)
                {
                    playerUnitsOnField.Add(unit);
                }
            }
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt) => TrackPlayerUnitSpawn(evt.Unit);

        private void TrackPlayerUnitSpawn(AbstractUnit unit)
        {
            if (unit == null || unit.Owner != hudOwner)
            {
                return;
            }

            playerUnitsOnField.Add(unit);
            RefreshPopulationHud();
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit == null)
            {
                return;
            }

            if (evt.Unit != null && evt.Unit.Owner == hudOwner)
            {
                playerUnitsOnField.Remove(evt.Unit);
                RefreshPopulationHud();
            }
        }

        /// <summary>
        /// Mục tiêu: Cập nhật số unit human local đang sống trên Population Container (HUD).
        /// Cách hoạt động: Đếm HashSet, ghi vào Population và populationText giống stone/wood/food.
        /// </summary>
        void RefreshPopulationHud()
        {
            EnsureDictionariesInitialized();
            if (Population == null || !Population.ContainsKey(hudOwner))
            {
                return;
            }

            playerUnitsOnField.RemoveWhere(unit => unit == null);

            int aliveCount = 0;
            foreach (AbstractUnit unit in playerUnitsOnField)
            {
                if (unit != null && unit.CurrentHealth > 0)
                {
                    aliveCount++;
                }
            }
            Population[hudOwner] = aliveCount;

            if (populationText == null)
            {
                return;
            }

            int limit = PopulationLimit[hudOwner];
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
            if (activeInstance == this)
            {
                activeInstance = null;
                busHandlersRegistered = false;
            }

            if (busHandlersRegistered)
            {
                Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
                Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
                Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
                busHandlersRegistered = false;
            }
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null)
            {
                return;
            }

            EnsureDictionariesInitialized();

            if (evt.Supply.Equals(stoneSO))
            {
                Stone[evt.Owner] += evt.Amount;
                if (hudOwner == evt.Owner && stoneText != null)
                {
                    stoneText.SetText(Stone[evt.Owner].ToString());
                }
            }
            else if (evt.Supply.Equals(woodSO))
            {
                Wood[evt.Owner] += evt.Amount;
                if (hudOwner == evt.Owner && woodText != null)
                {
                    woodText.SetText(Wood[evt.Owner].ToString());
                }
            }
            else if (evt.Supply.Equals(foodSO))
            {
                Food[evt.Owner] += evt.Amount;
                if (hudOwner == evt.Owner && foodText != null)
                {
                    foodText.SetText(Food[evt.Owner].ToString());
                }
            }
        }
    }
}
