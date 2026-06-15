using System.Collections.Generic;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Ánh xạ tên asset UnlockableSO/BuildingSO → instance runtime cho lệnh MP.
    /// </summary>
    public static class RtsUnlockableAssetCatalog
    {
        static readonly Dictionary<string, UnlockableSO> UnlockablesByName = new();
        static readonly Dictionary<string, BuildingSO> BuildingsByName = new();
        static readonly HashSet<GameObject> RegisteredSpawnPrefabs = new();

        public static void Clear()
        {
            UnlockablesByName.Clear();
            BuildingsByName.Clear();
            RegisteredSpawnPrefabs.Clear();
        }

        /// <summary>
        /// Mục tiêu: Nạp catalog khi server vào scene trận.
        /// Cách hoạt động: Gộp mảng setup + prefab spawn + prefab từ mỗi UnlockableSO.
        /// </summary>
        public static void InitializeFromSetup(RtsUtsGameSceneSetup setup)
        {
            Clear();
            if (setup == null)
            {
                return;
            }

            RegisterUnlockables(setup.unlockableCatalog);
            RegisterAllLoadedUnlockables();

            RegisterPrefab(setup.civilCentralPrefab);
            RegisterPrefab(setup.startingWorkerPrefab);
            RegisterPrefabs(setup.additionalNetworkSpawnPrefabs);

            NetworkManager manager = NetworkManager.singleton;
            if (manager != null)
            {
                for (int i = 0; i < manager.spawnPrefabs.Count; i++)
                {
                    RegisterPrefab(manager.spawnPrefabs[i]);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Tự nạp mọi UnlockableSO đã load trong memory (train/build/research) mà không cần kéo thủ công từng asset.
        /// </summary>
        static void RegisterAllLoadedUnlockables()
        {
            UnlockableSO[] loaded = Resources.FindObjectsOfTypeAll<UnlockableSO>();
            for (int i = 0; i < loaded.Length; i++)
            {
                RegisterUnlockable(loaded[i]);
            }
        }

        public static void RegisterUnlockables(UnlockableSO[] unlockables)
        {
            if (unlockables == null)
            {
                return;
            }

            for (int i = 0; i < unlockables.Length; i++)
            {
                RegisterUnlockable(unlockables[i]);
            }
        }

        public static void RegisterUnlockable(UnlockableSO unlockable)
        {
            if (unlockable == null || string.IsNullOrWhiteSpace(unlockable.name))
            {
                return;
            }

            if (UnlockablesByName.ContainsKey(unlockable.name))
            {
                return;
            }

            UnlockablesByName[unlockable.name] = unlockable;
            if (unlockable is BuildingSO buildingSo)
            {
                BuildingsByName[buildingSo.name] = buildingSo;
                RegisterPrefab(buildingSo.Prefab);
            }
            else if (unlockable is AbstractUnitSO unitSo)
            {
                RegisterPrefab(unitSo.Prefab);
            }
        }

        public static void RegisterPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null)
            {
                return;
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                RegisterPrefab(prefabs[i]);
            }
        }

        public static void RegisterPrefab(GameObject prefab)
        {
            if (prefab == null || RegisteredSpawnPrefabs.Contains(prefab))
            {
                return;
            }

            RegisteredSpawnPrefabs.Add(prefab);
            if (prefab.TryGetComponent(out AbstractUnit unit) && unit.UnitSO is UnlockableSO unlockable)
            {
                RegisterUnlockable(unlockable);
            }
            else if (prefab.TryGetComponent(out BaseBuilding building) && building.BuildingSO != null)
            {
                RegisterUnlockable(building.BuildingSO);
            }
        }

        public static bool TryResolveUnlockable(string assetName, out UnlockableSO unlockable)
        {
            unlockable = null;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            return UnlockablesByName.TryGetValue(assetName.Trim(), out unlockable);
        }

        public static bool TryResolveBuilding(string assetName, out BuildingSO buildingSo)
        {
            buildingSo = null;
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            if (BuildingsByName.TryGetValue(assetName.Trim(), out buildingSo))
            {
                return true;
            }

            if (TryResolveUnlockable(assetName, out UnlockableSO unlockable) && unlockable is BuildingSO resolved)
            {
                buildingSo = resolved;
                return true;
            }

            return false;
        }

        public static IEnumerable<GameObject> EnumerateRegisteredPrefabs() => RegisteredSpawnPrefabs;
    }
}
