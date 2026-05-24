using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Snapshot read-only cho một tick — gom registry + <see cref="Supplies"/> static.
    /// </summary>
    public sealed class AIWorldStateSnapshot
    {
        public Owner Owner { get; }
        public IReadOnlyList<AbstractUnit> Units { get; }
        public IReadOnlyList<Worker> Workers { get; }
        public IReadOnlyList<AbstractUnit> MilitaryUnits { get; }
        public IReadOnlyList<BaseBuilding> Buildings { get; }
        public BaseBuilding CivilCentral { get; }
        public IReadOnlyList<GatherableSupply> GatherableSupplies { get; }
        public int Stone { get; }
        public int Wood { get; }
        public int Food { get; }
        public int Population { get; }
        public int PopulationLimit { get; }
        /// <summary>Tổng lính (không Worker) đã spawn — không giảm khi chết.</summary>
        public int LifetimeMilitarySpawnCount { get; }

        internal AIWorldStateSnapshot(
            Owner owner,
            IReadOnlyList<AbstractUnit> units,
            IReadOnlyList<Worker> workers,
            IReadOnlyList<AbstractUnit> militaryUnits,
            IReadOnlyList<BaseBuilding> buildings,
            BaseBuilding civilCentral,
            IReadOnlyList<GatherableSupply> gatherableSupplies,
            int stone,
            int wood,
            int food,
            int population,
            int populationLimit,
            int lifetimeMilitarySpawnCount)
        {
            Owner = owner;
            Units = units;
            Workers = workers;
            MilitaryUnits = militaryUnits;
            Buildings = buildings;
            CivilCentral = civilCentral;
            GatherableSupplies = gatherableSupplies;
            Stone = stone;
            Wood = wood;
            Food = food;
            Population = population;
            PopulationLimit = populationLimit;
            LifetimeMilitarySpawnCount = lifetimeMilitarySpawnCount;
        }
    }

    /// <summary>
    /// Mục tiêu: Tạo snapshot từ <see cref="AIUnitRegistry"/> mỗi tick cho planner.
    /// Cách hoạt động: Tái sử dụng list nội bộ — không ToArray mỗi tick (giảm GC spike).
    /// </summary>
    public class AIWorldState
    {
        private readonly List<AbstractUnit> units = new(64);
        private readonly List<Worker> workers = new(32);
        private readonly List<AbstractUnit> militaryUnits = new(48);
        private readonly List<BaseBuilding> buildings = new(32);
        private readonly List<GatherableSupply> gatherableSupplies = new(64);

        public AIWorldStateSnapshot LastSnapshot { get; private set; }

        /// <summary>
        /// Mục tiêu: Snapshot mới nhất cho managers trong tick hiện tại (chỉ hợp lệ đến tick kế).
        /// Cách hoạt động: Clear buffer; copy registry; phân loại Worker; đọc Supplies[owner].
        /// </summary>
        public AIWorldStateSnapshot BuildSnapshot(AIUnitRegistry registry, Owner owner)
        {
            units.Clear();
            workers.Clear();
            militaryUnits.Clear();
            buildings.Clear();
            gatherableSupplies.Clear();

            foreach (AbstractUnit unit in registry.Units)
            {
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                units.Add(unit);
                if (unit is Worker worker)
                {
                    workers.Add(worker);
                }
                else
                {
                    militaryUnits.Add(unit);
                }
            }

            BaseBuilding civilCentral = null;
            foreach (BaseBuilding building in registry.Buildings)
            {
                if (building == null)
                {
                    continue;
                }

                buildings.Add(building);
                if (civilCentral == null && CivilCentralUtility.IsCivilCentral(building))
                {
                    civilCentral = building;
                }
            }

            foreach (GatherableSupply supply in registry.GatherableSupplies)
            {
                if (supply != null && supply.Amount > 0)
                {
                    gatherableSupplies.Add(supply);
                }
            }

            int stone = ReadSupply(Supplies.Stone, owner);
            int wood = ReadSupply(Supplies.Wood, owner);
            int food = ReadSupply(Supplies.Food, owner);
            int population = ReadSupply(Supplies.Population, owner);
            int populationLimit = ReadSupply(Supplies.PopulationLimit, owner);

            LastSnapshot = new AIWorldStateSnapshot(
                owner,
                units,
                workers,
                militaryUnits,
                buildings,
                civilCentral,
                gatherableSupplies,
                stone,
                wood,
                food,
                population,
                populationLimit,
                registry.LifetimeMilitarySpawnCount);

            return LastSnapshot;
        }

        private static int ReadSupply(System.Collections.Generic.Dictionary<Owner, int> table, Owner owner)
        {
            if (table == null || !table.TryGetValue(owner, out int value))
            {
                return 0;
            }

            return value;
        }
    }
}
