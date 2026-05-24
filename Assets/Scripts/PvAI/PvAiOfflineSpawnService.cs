using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Spawn Civil Central + worker khởi đầu cho human và AI khi Play PvE offline.
    /// </summary>
    public static class PvAiOfflineSpawnService
    {
        const string CivilCentralNameToken = "civil_central";

        /// <summary>
        /// Mục tiêu: Khởi tạo trận PvE một lần khi vào Game 1.
        /// Cách hoạt động: Xóa CC cắm sẵn (nếu bật) → spawn 2 phe tại spawn points.
        /// </summary>
        public static void SpawnMatch(PvAiGameSceneSetup setup)
        {
            if (setup == null)
            {
                Debug.LogError("[PvAiOfflineSpawnService] Thiếu PvAiGameSceneSetup.");
                return;
            }

            if (setup.destroyScenePlacedCivilCentrals)
            {
                RemoveScenePlacedCivilCentrals();
            }

            SpawnFaction(setup, 0, setup.humanOwner);
            SpawnFaction(setup, 1, setup.aiOwner);
        }

        static void SpawnFaction(PvAiGameSceneSetup setup, int spawnIndex, Owner owner)
        {
            if (setup.factionSpawnPoints == null
                || spawnIndex < 0
                || spawnIndex >= setup.factionSpawnPoints.Length
                || setup.factionSpawnPoints[spawnIndex] == null)
            {
                Debug.LogWarning($"[PvAiOfflineSpawnService] Thiếu spawn point index {spawnIndex}.");
                return;
            }

            Vector3 spawn = setup.factionSpawnPoints[spawnIndex].position;
            Quaternion rotation = setup.factionSpawnPoints[spawnIndex].rotation;

            if (setup.civilCentralPrefab != null)
            {
                PvAiOfflineEntityFactory.Spawn(setup.civilCentralPrefab, spawn, rotation, owner);
            }

            if (setup.spawnStartingWorker && setup.startingWorkerPrefab != null)
            {
                SpawnStartingWorkers(setup, spawn, rotation, owner);
            }
        }

        static void SpawnStartingWorkers(
            PvAiGameSceneSetup setup,
            Vector3 baseSpawn,
            Quaternion rotation,
            Owner owner)
        {
            int count = StartingWorkerSpawnLayout.ClampCount(setup.startingWorkerCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 workerPos = StartingWorkerSpawnLayout.GetPosition(
                    baseSpawn,
                    setup.workerOffsetFromBase,
                    setup.workerSpawnSpacing,
                    i);

                PvAiOfflineEntityFactory.Spawn(
                    setup.startingWorkerPrefab,
                    workerPos,
                    rotation,
                    owner);
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh trùng CC cũ (scene copy / MP prep) với bản spawn runtime.
        /// Cách hoạt động: Destroy mọi root object tên chứa civil_central trong scene active.
        /// </summary>
        public static void RemoveScenePlacedCivilCentrals()
        {
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                if (!IsScenePlacedCivilCentral(building.gameObject.name))
                {
                    continue;
                }

                Object.Destroy(building.gameObject);
            }
        }

        static bool IsScenePlacedCivilCentral(string objectName) =>
            !string.IsNullOrEmpty(objectName)
            && objectName.ToLowerInvariant().Contains(CivilCentralNameToken);
    }
}
