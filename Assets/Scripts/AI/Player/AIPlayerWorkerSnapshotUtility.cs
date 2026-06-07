using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Snapshot world state tối thiểu cho player/voice chọn worker — cùng dữ liệu AI planner dùng.
    /// </summary>
    public static class AIPlayerWorkerSnapshotUtility
    {
        static readonly List<AbstractUnit> unitsScratch = new(64);
        static readonly List<Worker> workersScratch = new(32);
        static readonly List<AbstractUnit> militaryScratch = new(48);
        static readonly List<BaseBuilding> buildingsScratch = new(32);
        static readonly List<GatherableSupply> suppliesScratch = new(0);

        /// <summary>
        /// Mục tiêu: Tạo snapshot cho một Owner từ scene hiện tại (không cần AIUnitRegistry).
        /// Cách hoạt động: Quét unit/nhà; lọc owner + còn sống; gán Civil Central cho anchor.
        /// </summary>
        public static bool TryBuildSnapshot(Owner owner, out AIWorldStateSnapshot snapshot)
        {
            snapshot = null;
            if (owner == Owner.Invalid)
            {
                return false;
            }

            unitsScratch.Clear();
            workersScratch.Clear();
            militaryScratch.Clear();
            buildingsScratch.Clear();

            AbstractUnit[] allUnits = Object.FindObjectsByType<AbstractUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < allUnits.Length; i++)
            {
                AbstractUnit unit = allUnits[i];
                if (unit == null || unit.Owner != owner || unit.CurrentHealth <= 0)
                {
                    continue;
                }

                unitsScratch.Add(unit);
                if (unit is Worker worker)
                {
                    workersScratch.Add(worker);
                }
                else
                {
                    militaryScratch.Add(unit);
                }
            }

            BaseBuilding civilCentral = null;
            BaseBuilding[] allBuildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < allBuildings.Length; i++)
            {
                BaseBuilding building = allBuildings[i];
                if (building == null || building.Owner != owner)
                {
                    continue;
                }

                buildingsScratch.Add(building);
                if (civilCentral == null && CivilCentralUtility.IsCivilCentral(building))
                {
                    civilCentral = building;
                }
            }

            snapshot = new AIWorldStateSnapshot(
                owner,
                unitsScratch,
                workersScratch,
                militaryScratch,
                buildingsScratch,
                civilCentral,
                suppliesScratch,
                ReadSupply(Supplies.Stone, owner),
                ReadSupply(Supplies.Wood, owner),
                ReadSupply(Supplies.Food, owner),
                ReadSupply(Supplies.Population, owner),
                ReadSupply(Supplies.PopulationLimit, owner),
                lifetimeMilitarySpawnCount: 0);

            return true;
        }

        /// <summary>
        /// Mục tiêu: Điểm neo khi AI chọn worker gather/build redirect (thường là Civil Central).
        /// Cách hoạt động: CC → worker đầu tiên → Vector3.zero.
        /// </summary>
        public static Vector3 GetEconomyAnchor(AIWorldStateSnapshot snapshot)
        {
            if (snapshot?.CivilCentral != null)
            {
                return snapshot.CivilCentral.transform.position;
            }

            if (snapshot != null && snapshot.Workers.Count > 0 && snapshot.Workers[0] != null)
            {
                return snapshot.Workers[0].transform.position;
            }

            return Vector3.zero;
        }

        static int ReadSupply(Dictionary<Owner, int> table, Owner owner)
        {
            if (table == null || !table.TryGetValue(owner, out int value))
            {
                return 0;
            }

            return value;
        }
    }
}
