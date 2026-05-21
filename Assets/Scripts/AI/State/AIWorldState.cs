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
            int populationLimit)
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
        }
    }

    /// <summary>
    /// Mục tiêu: Tạo snapshot từ <see cref="AIUnitRegistry"/> mỗi tick cho planner.
    /// Cách hoạt động: Lọc worker/military, tìm Civil Central, đọc Supplies dictionaries.
    /// </summary>
    public class AIWorldState
    {
        private static readonly List<AbstractUnit> UnitScratch = new(64);
        private static readonly List<Worker> WorkerScratch = new(32);
        private static readonly List<AbstractUnit> MilitaryScratch = new(48);
        private static readonly List<BaseBuilding> BuildingScratch = new(32);
        private static readonly List<GatherableSupply> SupplyScratch = new(64);

        public AIWorldStateSnapshot LastSnapshot { get; private set; }

        /// <summary>
        /// Mục tiêu: Snapshot mới nhất cho managers trong tick hiện tại.
        /// Cách hoạt động: Copy registry collections; phân loại Worker; đọc Supplies[owner].
        /// </summary>
        public AIWorldStateSnapshot BuildSnapshot(AIUnitRegistry registry, Owner owner)
        {
            UnitScratch.Clear();
            WorkerScratch.Clear();
            MilitaryScratch.Clear();
            BuildingScratch.Clear();
            SupplyScratch.Clear();

            foreach (AbstractUnit unit in registry.Units)
            {
                if (unit == null || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                UnitScratch.Add(unit);
                if (unit is Worker worker)
                {
                    WorkerScratch.Add(worker);
                }
                else
                {
                    MilitaryScratch.Add(unit);
                }
            }

            BaseBuilding civilCentral = null;
            foreach (BaseBuilding building in registry.Buildings)
            {
                if (building == null)
                {
                    continue;
                }

                BuildingScratch.Add(building);
                if (civilCentral == null && CivilCentralUtility.IsCivilCentral(building))
                {
                    civilCentral = building;
                }
            }

            foreach (GatherableSupply supply in registry.GatherableSupplies)
            {
                if (supply != null && supply.Amount > 0)
                {
                    SupplyScratch.Add(supply);
                }
            }

            int stone = ReadSupply(Supplies.Stone, owner);
            int wood = ReadSupply(Supplies.Wood, owner);
            int food = ReadSupply(Supplies.Food, owner);
            int population = ReadSupply(Supplies.Population, owner);
            int populationLimit = ReadSupply(Supplies.PopulationLimit, owner);

            LastSnapshot = new AIWorldStateSnapshot(
                owner,
                UnitScratch.ToArray(),
                WorkerScratch.ToArray(),
                MilitaryScratch.ToArray(),
                BuildingScratch.ToArray(),
                civilCentral,
                SupplyScratch.ToArray(),
                stone,
                wood,
                food,
                population,
                populationLimit);

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
