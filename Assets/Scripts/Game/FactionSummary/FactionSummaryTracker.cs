using System;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Game.FactionSummary
{
    /// <summary>
    /// SRP: Theo dõi thống kê tài nguyên / unit / công trình theo Owner qua EventBus.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionSummaryTracker : MonoBehaviour
    {
        sealed class OwnerStats
        {
            public int StoneSpent;
            public int WoodSpent;
            public int FoodSpent;
            public int UnitsAlive;
            public int UnitsTotal;
            public int BuildingsAlive;
            public int BuildingsTotal;
            public readonly Dictionary<string, TypeCount> UnitsByType = new(StringComparer.Ordinal);
            public readonly Dictionary<string, TypeCount> BuildingsByType = new(StringComparer.Ordinal);
        }

        struct TypeCount
        {
            public int Alive;
            public int Total;
        }

        static FactionSummaryTracker instance;
        static bool blockAutoCreate;

        readonly Dictionary<Owner, OwnerStats> statsByOwner = new();
        readonly HashSet<int> registeredUnitIds = new(256);
        readonly HashSet<int> registeredBuildingIds = new(128);
        bool busRegistered;
        bool backfillDone;

        public static FactionSummaryTracker Instance => instance;

        /// <summary>
        /// Mục tiêu: Tracker luôn sẵn sàng trong scene gameplay.
        /// Cách hoạt động: Tìm instance hoặc tạo DontDestroyOnLoad host nếu chưa có.
        /// </summary>
        public static FactionSummaryTracker EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            FactionSummaryTracker existing = FindFirstObjectByType<FactionSummaryTracker>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            if (blockAutoCreate || !CanCreateRuntimeInstance())
            {
                return null;
            }

            var host = new GameObject(nameof(FactionSummaryTracker));
            return host.AddComponent<FactionSummaryTracker>();
        }

        /// <summary>
        /// Mục tiêu: Hủy tracker DontDestroyOnLoad sau khi đã chụp snapshot kết thúc trận.
        /// Cách hoạt động: Chặn tạo mới, unregister bus, Destroy GameObject.
        /// </summary>
        public static void DestroyRuntimeInstance()
        {
            blockAutoCreate = true;

            if (instance == null)
            {
                return;
            }

            FactionSummaryTracker tracker = instance;
            instance = null;
            if (tracker != null)
            {
                tracker.UnregisterBusHandlers();
                Destroy(tracker.gameObject);
            }
        }

        /// <summary>
        /// Mục tiêu: Cho phép tạo tracker mới khi vào trận gameplay tiếp theo.
        /// </summary>
        public static void ResetForNewGameplay()
        {
            blockAutoCreate = false;
        }

        static bool CanCreateRuntimeInstance()
        {
            return GameplayStartupScenes.IsActiveGameplayScene();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticsForNewPlaySession()
        {
            instance = null;
            blockAutoCreate = false;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOwnerStats();
        }

        void OnEnable()
        {
            RegisterBusHandlers();
            if (!backfillDone)
            {
                BackfillExistingEntities();
                backfillDone = true;
            }
        }

        void OnDisable()
        {
            UnregisterBusHandlers();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Mục tiêu: Cung cấp dữ liệu cho scroll view tóm tắt.
        /// Cách hoạt động: Đọc Supplies static + spent/alive/total đã tích lũy theo Owner.
        /// </summary>
        public void BuildSnapshot(Owner owner, FactionSummarySnapshot target)
        {
            if (target == null)
            {
                return;
            }

            target.Clear();
            Supplies.EnsureReady();

            if (!statsByOwner.TryGetValue(owner, out OwnerStats stats))
            {
                stats = new OwnerStats();
                statsByOwner[owner] = stats;
            }

            int stoneCurrent = ReadSupply(Supplies.Stone, owner);
            int woodCurrent = ReadSupply(Supplies.Wood, owner);
            int foodCurrent = ReadSupply(Supplies.Food, owner);

            FactionSummarySection resources = target.BeginSection("Tài nguyên");
            resources.AddEntry("Đá", stoneCurrent, stoneCurrent + stats.StoneSpent);
            resources.AddEntry("Gỗ", woodCurrent, woodCurrent + stats.WoodSpent);
            resources.AddEntry("Thực phẩm", foodCurrent, foodCurrent + stats.FoodSpent);

            FactionSummarySection units = target.BeginSection("Đơn vị");
            units.AddEntry("Tổng", stats.UnitsAlive, stats.UnitsTotal);
            AppendTypeEntries(units, stats.UnitsByType);

            FactionSummarySection buildings = target.BeginSection("Công trình");
            buildings.AddEntry("Tổng", stats.BuildingsAlive, stats.BuildingsTotal);
            AppendTypeEntries(buildings, stats.BuildingsByType);
        }

        static void AppendTypeEntries(FactionSummarySection section, Dictionary<string, TypeCount> byType)
        {
            if (byType.Count == 0)
            {
                return;
            }

            var keys = new List<string>(byType.Keys);
            keys.Sort(StringComparer.Ordinal);

            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                TypeCount count = byType[key];
                if (count.Total <= 0)
                {
                    continue;
                }

                section.AddEntry(key, count.Alive, count.Total);
            }
        }

        static int ReadSupply(Dictionary<Owner, int> table, Owner owner)
        {
            if (table == null || !table.ContainsKey(owner))
            {
                return 0;
            }

            return table[owner];
        }

        void EnsureOwnerStats()
        {
            foreach (Owner owner in Enum.GetValues(typeof(Owner)))
            {
                if (!statsByOwner.ContainsKey(owner))
                {
                    statsByOwner[owner] = new OwnerStats();
                }
            }
        }

        void RegisterBusHandlers()
        {
            if (busRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.RegisterForAll(HandleSupplyEvent);
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
            busRegistered = true;
        }

        void UnregisterBusHandlers()
        {
            if (!busRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.UnregisterForAll(HandleSupplyEvent);
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
            busRegistered = false;
        }

        void BackfillExistingEntities()
        {
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                RegisterUnitSpawn(units[i]);
            }

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                RegisterBuildingSpawn(buildings[i]);
            }
        }

        void HandleSupplyEvent(SupplyEvent evt)
        {
            if (evt.Supply == null || evt.Amount >= 0)
            {
                return;
            }

            if (!statsByOwner.TryGetValue(evt.Owner, out OwnerStats stats))
            {
                stats = new OwnerStats();
                statsByOwner[evt.Owner] = stats;
            }

            int spent = Mathf.Abs(evt.Amount);
            string supplyName = evt.Supply.name;
            if (supplyName.Contains("Stone", StringComparison.OrdinalIgnoreCase))
            {
                stats.StoneSpent += spent;
            }
            else if (supplyName.Contains("Wood", StringComparison.OrdinalIgnoreCase))
            {
                stats.WoodSpent += spent;
            }
            else if (supplyName.Contains("Food", StringComparison.OrdinalIgnoreCase))
            {
                stats.FoodSpent += spent;
            }
        }

        void HandleUnitSpawn(UnitSpawnEvent evt) => RegisterUnitSpawn(evt.Unit);

        void HandleUnitDeath(UnitDeathEvent evt)
        {
            AbstractUnit unit = evt.Unit;
            if (unit == null)
            {
                return;
            }

            if (!statsByOwner.TryGetValue(unit.Owner, out OwnerStats stats))
            {
                return;
            }

            if (stats.UnitsAlive > 0)
            {
                stats.UnitsAlive--;
            }

            string typeName = FactionSummaryDisplayNames.ResolveUnit(unit);
            if (stats.UnitsByType.TryGetValue(typeName, out TypeCount typeCount) && typeCount.Alive > 0)
            {
                typeCount.Alive--;
                stats.UnitsByType[typeName] = typeCount;
            }
        }

        void HandleBuildingSpawn(BuildingSpawnEvent evt) => RegisterBuildingSpawn(evt.Building);

        void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            BaseBuilding building = evt.Building;
            if (building == null)
            {
                return;
            }

            Owner owner = evt.Owner != Owner.Invalid ? evt.Owner : building.Owner;
            if (!statsByOwner.TryGetValue(owner, out OwnerStats stats))
            {
                return;
            }

            if (stats.BuildingsAlive > 0)
            {
                stats.BuildingsAlive--;
            }

            string typeName = FactionSummaryDisplayNames.ResolveBuilding(building);
            if (stats.BuildingsByType.TryGetValue(typeName, out TypeCount typeCount) && typeCount.Alive > 0)
            {
                typeCount.Alive--;
                stats.BuildingsByType[typeName] = typeCount;
            }
        }

        void RegisterUnitSpawn(AbstractUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            int id = unit.gameObject.GetInstanceID();
            if (!registeredUnitIds.Add(id))
            {
                return;
            }

            if (!statsByOwner.TryGetValue(unit.Owner, out OwnerStats stats))
            {
                stats = new OwnerStats();
                statsByOwner[unit.Owner] = stats;
            }

            bool isAlive = unit.CurrentHealth > 0;
            stats.UnitsTotal++;
            if (isAlive)
            {
                stats.UnitsAlive++;
            }

            string typeName = FactionSummaryDisplayNames.ResolveUnit(unit);
            if (!stats.UnitsByType.TryGetValue(typeName, out TypeCount typeCount))
            {
                typeCount = default;
            }

            typeCount.Total++;
            if (isAlive)
            {
                typeCount.Alive++;
            }

            stats.UnitsByType[typeName] = typeCount;
        }

        void RegisterBuildingSpawn(BaseBuilding building)
        {
            if (building == null)
            {
                return;
            }

            int id = building.gameObject.GetInstanceID();
            if (!registeredBuildingIds.Add(id))
            {
                return;
            }

            if (!statsByOwner.TryGetValue(building.Owner, out OwnerStats stats))
            {
                stats = new OwnerStats();
                statsByOwner[building.Owner] = stats;
            }

            bool isAlive = building.CurrentHealth > 0
                && building.Progress.State != BuildingProgress.BuildingState.Destroyed;
            stats.BuildingsTotal++;
            if (isAlive)
            {
                stats.BuildingsAlive++;
            }

            string typeName = FactionSummaryDisplayNames.ResolveBuilding(building);
            if (!stats.BuildingsByType.TryGetValue(typeName, out TypeCount typeCount))
            {
                typeCount = default;
            }

            typeCount.Total++;
            if (isAlive)
            {
                typeCount.Alive++;
            }

            stats.BuildingsByType[typeName] = typeCount;
        }

        /// <summary>
        /// Mục tiêu: Tìm phe đối thủ cho panel kết thúc (MP P1/P2 hoặc PvE AI).
        /// Cách hoạt động: Ưu tiên human còn dữ liệu; fallback AI có unit/building; cuối cùng P2/AI2.
        /// </summary>
        public Owner FindOpponentOwner(Owner localOwner)
        {
            Owner humanOpponent = localOwner == Owner.Player1 ? Owner.Player2 : Owner.Player1;
            if (humanOpponent != localOwner && HasTrackedActivity(humanOpponent))
            {
                return humanOpponent;
            }

            Owner best = Owner.Invalid;
            int bestScore = -1;
            foreach (System.Collections.Generic.KeyValuePair<Owner, OwnerStats> pair in statsByOwner)
            {
                Owner candidate = pair.Key;
                if (candidate == localOwner || candidate == Owner.Invalid || candidate == Owner.Unowned)
                {
                    continue;
                }

                int score = GetActivityScore(pair.Value);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best != Owner.Invalid && bestScore > 0)
            {
                return best;
            }

            return humanOpponent != localOwner ? humanOpponent : Owner.AI2;
        }

        bool HasTrackedActivity(Owner owner)
        {
            return statsByOwner.TryGetValue(owner, out OwnerStats stats) && GetActivityScore(stats) > 0;
        }

        static int GetActivityScore(OwnerStats stats)
        {
            if (stats == null)
            {
                return 0;
            }

            return stats.UnitsTotal + stats.BuildingsTotal;
        }
    }
}
