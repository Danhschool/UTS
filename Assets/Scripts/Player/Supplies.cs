using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;
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

        static readonly List<Supplies> EnabledHudInstances = new(4);
        static bool busHandlersRegistered;
        static bool applyingNetworkSnapshot;

        /// <summary>
        /// Mục tiêu: Đồng bộ HUD dân sau khi unit Player1 spawn xong (Start / NotifySpawned).
        /// </summary>
        public static void RegisterPlayerUnit(AbstractUnit unit)
        {
            for (int i = 0; i < EnabledHudInstances.Count; i++)
            {
                EnabledHudInstances[i]?.TrackPlayerUnitSpawn(unit);
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh NullRef khi PlayerViewBinder/FactionHudBinder gọi trước Supplies.Awake (UI chưa active).
        /// Cách hoạt động: Tạo dictionary static một lần với giá trị mặc định cho mọi Owner.
        /// </summary>
        /// <summary>
        /// Mục tiêu: Module khác (ví dụ summary UI) đọc dictionary tài nguyên an toàn trước Awake HUD.
        /// </summary>
        public static void EnsureReady() => EnsureDictionariesInitialized();

        /// <summary>
        /// Mục tiêu: Client MP áp snapshot tài nguyên từ server (SyncVar hook).
        /// Cách hoạt động: Ghi dictionary static và refresh mọi HUD đang bật.
        /// </summary>
        public static void ApplyNetworkSnapshot(
            Owner owner,
            int stone,
            int wood,
            int food,
            int population,
            int populationLimit)
        {
            EnsureDictionariesInitialized();
            applyingNetworkSnapshot = true;
            try
            {
                Stone[owner] = stone;
                Wood[owner] = wood;
                Food[owner] = food;
                Population[owner] = population;
                PopulationLimit[owner] = populationLimit;
            }
            finally
            {
                applyingNetworkSnapshot = false;
            }

            for (int i = 0; i < EnabledHudInstances.Count; i++)
            {
                EnabledHudInstances[i]?.RefreshSupplyHud();
            }

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (owner == localOwner && HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                MpHudSuppliesResolver.FindForOwner(localOwner)?.BindHudOwner(localOwner);
            }
        }

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
                Stone.Add(owner, 1000);
                Wood.Add(owner, 1000);
                Food.Add(owner, 1000);
                Population.Add(owner, 0);
                PopulationLimit.Add(owner, 0);
            }
        }

        private void Awake()
        {
            EnsureDictionariesInitialized();
            if (hudOwner == Owner.Player1)
            {
                hudOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            }
        }

        void OnEnable()
        {
            EnsureDictionariesInitialized();
            RegisterHudInstance(this);
            RegisterBusHandlersIfNeeded();
            RegisterExistingHudOwnerUnits();
            RefreshSupplyHud();
        }

        void OnDisable()
        {
            UnregisterHudInstance(this);
        }

        static void RegisterHudInstance(Supplies instance)
        {
            if (instance == null || EnabledHudInstances.Contains(instance))
            {
                return;
            }

            EnabledHudInstances.Add(instance);
        }

        static void UnregisterHudInstance(Supplies instance)
        {
            if (instance == null)
            {
                return;
            }

            EnabledHudInstances.Remove(instance);
            if (EnabledHudInstances.Count == 0)
            {
                UnregisterBusHandlers();
            }
        }

        static void RegisterBusHandlersIfNeeded()
        {
            if (busHandlersRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.RegisterForAll(DispatchSupplyEvent);
            Bus<UnitSpawnEvent>.RegisterForAll(DispatchUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(DispatchUnitDeath);
            busHandlersRegistered = true;
        }

        static void UnregisterBusHandlers()
        {
            if (!busHandlersRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.UnregisterForAll(DispatchSupplyEvent);
            Bus<UnitSpawnEvent>.UnregisterForAll(DispatchUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(DispatchUnitDeath);
            busHandlersRegistered = false;
        }

        static void DispatchSupplyEvent(SupplyEvent evt)
        {
            for (int i = EnabledHudInstances.Count - 1; i >= 0; i--)
            {
                Supplies hud = EnabledHudInstances[i];
                if (hud == null)
                {
                    EnabledHudInstances.RemoveAt(i);
                    continue;
                }

                hud.HandleSupplyEvent(evt);
            }
        }

        static void DispatchUnitSpawn(UnitSpawnEvent evt)
        {
            for (int i = EnabledHudInstances.Count - 1; i >= 0; i--)
            {
                Supplies hud = EnabledHudInstances[i];
                if (hud == null)
                {
                    EnabledHudInstances.RemoveAt(i);
                    continue;
                }

                hud.HandleUnitSpawn(evt);
            }
        }

        static void DispatchUnitDeath(UnitDeathEvent evt)
        {
            for (int i = EnabledHudInstances.Count - 1; i >= 0; i--)
            {
                Supplies hud = EnabledHudInstances[i];
                if (hud == null)
                {
                    EnabledHudInstances.RemoveAt(i);
                    continue;
                }

                hud.HandleUnitDeath(evt);
            }
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

            RegisterHudInstance(this);
            RegisterBusHandlersIfNeeded();
            RegisterExistingHudOwnerUnits();
            RefreshSupplyHud();
        }

        internal void RefreshSupplyHud()
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

        void OnDestroy()
        {
            UnregisterHudInstance(this);
        }

        private void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null || applyingNetworkSnapshot)
            {
                return;
            }

            if (RtsNetplaySession.IsPureClient)
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
