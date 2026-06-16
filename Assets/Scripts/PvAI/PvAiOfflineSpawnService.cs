using System.Collections.Generic;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Spawn Civil Central + unit khởi đầu cho human và AI khi Play PvE offline.
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

            GameObject civilCentralInstance = null;
            if (setup.civilCentralPrefab != null)
            {
                civilCentralInstance = PvAiOfflineEntityFactory.Spawn(
                    setup.civilCentralPrefab,
                    spawn,
                    rotation,
                    owner);
            }

            if (setup.spawnStartingUnits)
            {
                SpawnStartingUnits(setup, civilCentralInstance, spawn, rotation, owner);
            }
        }

        /// <summary>
        /// Mục tiêu: Spawn mọi unit khởi đầu quanh rìa CC theo cấu hình Inspector.
        /// Cách hoạt động: Lấy anchor từ BaseBuilding.UnitSpawnWorldPosition; dàn slot theo spacing local CC.
        /// </summary>
        static void SpawnStartingUnits(
            PvAiGameSceneSetup setup,
            GameObject civilCentralInstance,
            Vector3 fallbackSpawn,
            Quaternion fallbackRotation,
            Owner owner)
        {
            List<StartingUnitSpawnEntry> entries = ResolveStartingUnitEntries(setup);
            if (entries.Count == 0)
            {
                return;
            }

            Vector3 anchor = fallbackSpawn;
            Quaternion anchorRotation = fallbackRotation;
            if (civilCentralInstance != null
                && civilCentralInstance.TryGetComponent(out BaseBuilding building))
            {
                anchor = building.UnitSpawnWorldPosition;
                anchorRotation = building.UnitSpawnWorldRotation;
            }

            int globalSlotIndex = 0;
            float centerSpacing = StartingWorkerSpawnLayout.ComputeCenterSpacing(setup.unitSpawnSphereRadius);
            Vector3 spacingLocal = new Vector3(centerSpacing, 0f, 0f);

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                StartingUnitSpawnEntry entry = entries[entryIndex];
                if (entry.unitPrefab == null)
                {
                    continue;
                }

                if (!PvAiStartingUnitPrefabValidator.TryValidate(entry.unitPrefab, out string prefabError))
                {
                    Debug.LogError($"[PvAiOfflineSpawnService] {prefabError}");
                    continue;
                }

                int count = StartingWorkerSpawnLayout.ClampCount(entry.count);
                for (int i = 0; i < count; i++)
                {
                    Vector3 unitPos = StartingWorkerSpawnLayout.GetPositionAtEdge(
                        anchor,
                        setup.unitSpawnFirstOffsetLocal,
                        spacingLocal,
                        anchorRotation,
                        globalSlotIndex);

                    PvAiOfflineEntityFactory.Spawn(
                        entry.unitPrefab,
                        unitPos,
                        anchorRotation,
                        owner);

                    globalSlotIndex++;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Gom cấu hình unit khởi đầu từ mảng mới hoặc field legacy trong scene cũ.
        /// Cách hoạt động: Ưu tiên startingUnits; nếu trống thì fallback startingWorkerPrefab/count.
        /// </summary>
        static List<StartingUnitSpawnEntry> ResolveStartingUnitEntries(PvAiGameSceneSetup setup)
        {
            var resolved = new List<StartingUnitSpawnEntry>(8);

            if (setup.startingUnits != null && setup.startingUnits.Length > 0)
            {
                for (int i = 0; i < setup.startingUnits.Length; i++)
                {
                    StartingUnitSpawnEntry entry = setup.startingUnits[i];
                    if (entry.unitPrefab == null || entry.count <= 0)
                    {
                        continue;
                    }

                    resolved.Add(entry);
                }

                return resolved;
            }

            if (setup.spawnStartingWorker && setup.startingWorkerPrefab != null)
            {
                resolved.Add(new StartingUnitSpawnEntry
                {
                    unitPrefab = setup.startingWorkerPrefab,
                    count = setup.startingWorkerCount
                });
            }

            return resolved;
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
