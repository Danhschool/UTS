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

            RtsUnlockableAssetCatalog.InitializeFromSetup(setup);

            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                return;
            }

            foreach (GameObject prefab in RtsUnlockableAssetCatalog.EnumerateRegisteredPrefabs())
            {
                RegisterPrefabOnManager(manager, prefab);
            }

            s_registeredForActiveMatch = true;
        }

        /// <summary>
        /// Mục tiêu: Reset khi rời trận / đổi scene để lần vào MP sau đăng ký lại catalog.
        /// Cách hoạt động: Chỉ xóa spawn registry — không reset cờ kết thúc trận (tránh Rpc trùng).
        /// </summary>
        public static void ResetMatchRegistration()
        {
            s_registeredForActiveMatch = false;
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
            out GameObject instance)
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

            EnsureNetworkComponents(instance);

            if (!instance.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                networkEntity = instance.AddComponent<RtsUtsNetworkEntity>();
            }

            networkEntity.ServerConfigure(connection != null ? connection.connectionId : -1, owner);

            RtsNetplaySimulationGate.ApplyToSpawnedEntity(instance);

            if (connection != null)
            {
                NetworkServer.Spawn(instance, connection);
            }
            else
            {
                NetworkServer.Spawn(instance);
            }

            return true;
        }

        static void RegisterPrefabOnManager(NetworkManager manager, GameObject prefab)
        {
            if (manager == null || prefab == null)
            {
                return;
            }

            if (!manager.spawnPrefabs.Contains(prefab))
            {
                manager.spawnPrefabs.Add(prefab);
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity) || identity.assetId == 0)
            {
                return;
            }

            if (NetworkClient.GetPrefab(identity.assetId, out _))
            {
                return;
            }

            NetworkClient.RegisterPrefab(prefab);
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

        static void EnsureNetworkComponents(GameObject instance)
        {
            if (!instance.TryGetComponent(out NetworkTransformUnreliable networkTransform))
            {
                networkTransform = instance.AddComponent<NetworkTransformUnreliable>();
            }

            ConfigureNetworkTransform(networkTransform, instance);

            if (!instance.TryGetComponent(out RtsUtsNetworkCombatSync _))
            {
                instance.AddComponent<RtsUtsNetworkCombatSync>();
            }

            if (instance.TryGetComponent(out BaseBuilding _)
                && !instance.TryGetComponent(out RtsUtsNetworkBuildingSync _))
            {
                instance.AddComponent<RtsUtsNetworkBuildingSync>();
            }
        }

        /// <summary>
        /// Mục tiêu: Giảm rubber-banding unit quân sự khi combat MP.
        /// Cách hoạt động: Tăng tần gửi transform cho unit; giữ mặc định cho nhà.
        /// </summary>
        static void ConfigureNetworkTransform(NetworkTransformUnreliable networkTransform, GameObject instance)
        {
            if (networkTransform == null || instance == null)
            {
                return;
            }

            if (instance.TryGetComponent(out AbstractUnit _))
            {
                networkTransform.syncInterval = 0.05f;
            }
        }
    }
}
