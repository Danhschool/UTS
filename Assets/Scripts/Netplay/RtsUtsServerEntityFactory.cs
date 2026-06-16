using GameDevTV.RTS.Game;
using GameDevTV.RTS.UI.InGame;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server spawn entity gameplay UTS (unit/building) với NetworkIdentity + RtsUtsNetworkEntity.
    /// </summary>
    public static class RtsUtsServerEntityFactory
    {
        static bool s_registeredForActiveMatch;
        static bool s_clientPrefabsRegistered;

        /// <summary>
        /// Mục tiêu: Đăng ký mọi prefab gameplay vào NetworkManager trước khi spawn runtime.
        /// Cách hoạt động: Một lần mỗi trận — nạp catalog, đăng ký Mirror spawn prefab.
        /// </summary>
        public static void EnsureAllGameplayPrefabsRegistered(RtsUtsGameSceneSetup setup)
        {
            if (s_registeredForActiveMatch)
            {
                return;
            }

            RegisterGameplayPrefabsFromSetup(setup);
            s_registeredForActiveMatch = true;
        }

        /// <summary>
        /// Mục tiêu: Pure client P2 nhận spawn nhà/unit runtime từ server (build/train).
        /// Cách hoạt động: Nạp catalog giống server, gọi NetworkClient.RegisterPrefab cho mọi prefab gameplay.
        /// </summary>
        public static void EnsureClientGameplayPrefabsRegistered(RtsUtsGameSceneSetup setup)
        {
            if (!NetworkClient.active || NetworkServer.active || s_clientPrefabsRegistered)
            {
                return;
            }

            if (setup == null)
            {
                return;
            }

            RegisterGameplayPrefabsFromSetup(setup);
            s_clientPrefabsRegistered = true;
        }

        static void RegisterGameplayPrefabsFromSetup(RtsUtsGameSceneSetup setup)
        {
            if (setup == null)
            {
                return;
            }

            RtsUnlockableAssetCatalog.InitializeFromSetup(setup);

            NetworkManager manager = NetworkManager.singleton;
            foreach (GameObject prefab in RtsUnlockableAssetCatalog.EnumerateRegisteredPrefabs())
            {
                RegisterPrefabOnManager(manager, prefab);
            }
        }

        /// <summary>
        /// Mục tiêu: Reset khi rời trận / đổi scene để lần vào MP sau đăng ký lại catalog.
        /// Cách hoạt động: Chỉ xóa spawn registry — không reset cờ kết thúc trận (tránh Rpc trùng).
        /// </summary>
        public static void ResetMatchRegistration()
        {
            s_registeredForActiveMatch = false;
            s_clientPrefabsRegistered = false;
            RtsUnlockableAssetCatalog.Clear();
        }

        public static bool TrySpawnUnit(
            AbstractUnitSO unitSo,
            Vector3 position,
            Quaternion rotation,
            Owner owner,
            NetworkConnectionToClient connection,
            out AbstractUnit unit)
        {
            unit = null;
            if (unitSo == null || unitSo.Prefab == null)
            {
                return false;
            }

            if (!TrySpawnGameplayEntity(unitSo.Prefab, position, rotation, owner, connection, out GameObject instance))
            {
                return false;
            }

            if (!instance.TryGetComponent(out unit))
            {
                Object.Destroy(instance);
                return false;
            }

            unit.Owner = owner;
            unit.NotifySpawned();
            return true;
        }

        public static bool TrySpawnBuilding(
            BuildingSO buildingSo,
            Vector3 position,
            Quaternion rotation,
            Owner owner,
            NetworkConnectionToClient connection,
            out BaseBuilding building)
        {
            building = null;
            if (buildingSo == null || buildingSo.Prefab == null)
            {
                return false;
            }

            if (!TrySpawnGameplayEntity(buildingSo.Prefab, position, rotation, owner, connection, out GameObject instance))
            {
                return false;
            }

            return instance.TryGetComponent(out building);
        }

        public static bool TrySpawnGameplayEntity(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Owner owner,
            NetworkConnectionToClient connection,
            out GameObject instance,
            System.Action<GameObject> configureBeforeSpawn = null)
        {
            instance = null;
            if (!RtsNetplaySession.ShouldRunAuthoritativeGameplay || prefab == null)
            {
                return false;
            }

            instance = Object.Instantiate(prefab, position, rotation);
            if (!instance.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"[RtsUtsServerEntityFactory] Prefab {prefab.name} thiếu NetworkIdentity.");
                Object.Destroy(instance);
                instance = null;
                return false;
            }

            if (!IsPrefabRegistered(prefab))
            {
                Debug.LogError(
                    $"[RtsUtsServerEntityFactory] Prefab {prefab.name} chưa có trong NetworkManager.spawnPrefabs.");
                Object.Destroy(instance);
                instance = null;
                return false;
            }

            RtsUtsGameplayNetworkComponents.ApplyToSpawnedInstance(instance);

            if (!instance.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                networkEntity = instance.AddComponent<RtsUtsNetworkEntity>();
                RtsNetplayNetworkIdentityUtility.RefreshBehaviours(instance);
            }

            networkEntity.ServerConfigure(connection != null ? connection.connectionId : -1, owner);

            RtsNetplaySimulationGate.ApplyToSpawnedEntity(instance);

            configureBeforeSpawn?.Invoke(instance);

            if (instance.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.EnsureNetworkSpawnCombatReady();
            }

            if (connection != null)
            {
                NetworkServer.Spawn(instance, connection);
            }
            else
            {
                NetworkServer.Spawn(instance);
            }

            if (instance.TryGetComponent(out RtsUtsNetworkCombatSync combatSync))
            {
                combatSync.PushFromCommandable();
            }

            if (instance.TryGetComponent(out RtsUtsNetworkBuildingSync buildingSync))
            {
                buildingSync.ServerPushFullState();
            }

            return true;
        }

        static void RegisterPrefabOnManager(NetworkManager manager, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            if (manager != null && !manager.spawnPrefabs.Contains(prefab))
            {
                manager.spawnPrefabs.Add(prefab);
            }

            RegisterPrefabOnClient(prefab);
        }

        static void RegisterPrefabOnClient(GameObject prefab)
        {
            if (prefab == null || !NetworkClient.active)
            {
                return;
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity) || identity.assetId == 0)
            {
                return;
            }

            NetworkClient.UnregisterPrefab(prefab);
            NetworkClient.RegisterPrefab(prefab, message => SpawnGameplayEntityOnClient(prefab, message), UnspawnGameplayEntityOnClient);
        }

        static GameObject SpawnGameplayEntityOnClient(GameObject prefab, SpawnMessage message)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, message.position, message.rotation);
            RtsUtsGameplayNetworkComponents.ApplyToSpawnedInstance(instance);
            return instance;
        }

        static void UnspawnGameplayEntityOnClient(GameObject instance)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }

        static bool IsPrefabRegistered(GameObject prefab)
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                return false;
            }

            if (manager.spawnPrefabs.Contains(prefab))
            {
                return true;
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                return false;
            }

            for (int i = 0; i < manager.spawnPrefabs.Count; i++)
            {
                GameObject candidate = manager.spawnPrefabs[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.TryGetComponent(out NetworkIdentity candidateId)
                    && candidateId.assetId == identity.assetId)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
