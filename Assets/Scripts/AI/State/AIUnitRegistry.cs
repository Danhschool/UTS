using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Cache unit/building/supply từ EventBus theo <see cref="Owner"/> — không Find trong tick.
    /// </summary>
    public class AIUnitRegistry
    {
        private readonly HashSet<AbstractUnit> units = new(64);
        private readonly HashSet<BaseBuilding> buildings = new(32);
        private readonly HashSet<GatherableSupply> gatherableSupplies = new(48);
        private Owner owner;
        private bool isSubscribed;

        public IReadOnlyCollection<AbstractUnit> Units => units;
        public IReadOnlyCollection<BaseBuilding> Buildings => buildings;
        public IReadOnlyCollection<GatherableSupply> GatherableSupplies => gatherableSupplies;

        /// <summary>
        /// Mục tiêu: Bắt đầu theo dõi owner và đồng bộ entity đã có trong scene.
        /// Cách hoạt động: Subscribe RegisterForAll; quét một lần FindObjectsByType (init only).
        /// </summary>
        public void Initialize(Owner targetOwner)
        {
            if (isSubscribed)
            {
                Dispose();
            }

            owner = targetOwner;
            units.Clear();
            buildings.Clear();
            gatherableSupplies.Clear();

            RegisterExistingInScene();
            Subscribe();
        }

        /// <summary>
        /// Mục tiêu: Gỡ handler khi AI tắt hoặc đổi owner.
        /// Cách hoạt động: UnregisterForAll mọi bus đã đăng ký.
        /// </summary>
        public void Dispose()
        {
            if (!isSubscribed)
            {
                return;
            }

            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
            Bus<SupplySpawnEvent>.UnregisterForAll(HandleSupplySpawn);
            Bus<SupplyDepletedEvent>.UnregisterForAll(HandleSupplyDepleted);
            isSubscribed = false;
        }

        private void Subscribe()
        {
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
            Bus<SupplySpawnEvent>.RegisterForAll(HandleSupplySpawn);
            Bus<SupplyDepletedEvent>.RegisterForAll(HandleSupplyDepleted);
            isSubscribed = true;
        }

        private void RegisterExistingInScene()
        {
            AbstractUnit[] existingUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < existingUnits.Length; i++)
            {
                TryAddUnit(existingUnits[i]);
            }

            BaseBuilding[] existingBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            for (int i = 0; i < existingBuildings.Length; i++)
            {
                TryAddBuilding(existingBuildings[i]);
            }

            GatherableSupply[] existingSupplies = Object.FindObjectsByType<GatherableSupply>(FindObjectsSortMode.None);
            for (int i = 0; i < existingSupplies.Length; i++)
            {
                TryAddSupply(existingSupplies[i]);
            }
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt) => TryAddUnit(evt.Unit);

        private void HandleUnitDeath(UnitDeathEvent evt) => TryRemoveUnit(evt.Unit);

        private void HandleBuildingSpawn(BuildingSpawnEvent evt) => TryAddBuilding(evt.Building);

        private void HandleBuildingDeath(BuildingDeathEvent evt) => TryRemoveBuilding(evt.Building);

        private void HandleSupplySpawn(SupplySpawnEvent evt) => TryAddSupply(evt.Supply);

        private void HandleSupplyDepleted(SupplyDepletedEvent evt) => TryRemoveSupply(evt.Supply);

        private void TryAddUnit(AbstractUnit unit)
        {
            if (unit == null || unit.Owner != owner)
            {
                return;
            }

            // Không lọc CurrentHealth ở đây: building gọi NotifySpawned() trước Start() (máu vẫn 0).
            // UnitDeathEvent sẽ gỡ unit chết khỏi cache.
            units.Add(unit);
        }

        private void TryRemoveUnit(AbstractUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            units.Remove(unit);
        }

        private void TryAddBuilding(BaseBuilding building)
        {
            if (building == null || building.Owner != owner)
            {
                return;
            }

            buildings.Add(building);
        }

        private void TryRemoveBuilding(BaseBuilding building)
        {
            if (building == null)
            {
                return;
            }

            buildings.Remove(building);
        }

        private void TryAddSupply(GatherableSupply supply)
        {
            if (supply == null || supply.Amount <= 0)
            {
                return;
            }

            gatherableSupplies.Add(supply);
        }

        private void TryRemoveSupply(GatherableSupply supply)
        {
            if (supply == null)
            {
                return;
            }

            gatherableSupplies.Remove(supply);
        }
    }
}
