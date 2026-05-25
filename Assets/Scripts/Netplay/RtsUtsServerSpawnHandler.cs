using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server spawn Civil Central + worker UTS theo team khi vào RtsNet_Game.
    /// </summary>
    public static class RtsUtsServerSpawnHandler
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsServerGameplayNotifier.OnSpawnTeamGameplay = HandleSpawnRequest;
        }

        static void HandleSpawnRequest(RtsServerSpawnRequest request)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            if (setup == null)
            {
                Debug.LogError("[RtsUtsServerSpawnHandler] Không tìm thấy RtsUtsGameSceneSetup trong scene.");
                return;
            }

            Owner owner = OwnerTeamMapping.FromTeamIndex(request.TeamIndex);
            Vector3 spawn = request.SpawnPosition;
            NetworkConnectionToClient connection = ResolveConnection(request.ConnectionId);

            int spawned = 0;

            if (setup.civilCentralPrefab != null)
            {
                if (SpawnEntity(setup.civilCentralPrefab, spawn, connection, owner))
                {
                    spawned++;
                }
            }

            if (setup.startingWorkerPrefab != null)
            {
                spawned += SpawnStartingWorkers(setup, spawn, connection, owner);
            }

            Debug.Log(
                $"[RtsUtsServerSpawnHandler] Team {request.TeamIndex} ({owner}): spawn {spawned} entity tại {spawn}.");
        }

        static NetworkConnectionToClient ResolveConnection(int connectionId)
        {
            return NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient connection)
                ? connection
                : null;
        }

        static int SpawnStartingWorkers(
            RtsUtsGameSceneSetup setup,
            Vector3 baseSpawn,
            NetworkConnectionToClient connection,
            Owner owner)
        {
            int count = StartingWorkerSpawnLayout.ClampCount(setup.startingWorkerCount);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                Vector3 workerPos = StartingWorkerSpawnLayout.GetPosition(
                    baseSpawn,
                    setup.workerOffsetFromBase,
                    setup.workerSpawnSpacing,
                    i);

                if (SpawnEntity(setup.startingWorkerPrefab, workerPos, connection, owner))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        static bool SpawnEntity(
            GameObject prefab,
            Vector3 position,
            NetworkConnectionToClient connection,
            Owner owner)
        {
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            if (!instance.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"[RtsUtsServerSpawnHandler] Prefab {prefab.name} thiếu NetworkIdentity.");
                Object.Destroy(instance);
                return false;
            }

            if (!IsPrefabRegistered(prefab))
            {
                Debug.LogError(
                    $"[RtsUtsServerSpawnHandler] Prefab {prefab.name} chưa có trong NetworkManager → Spawn Prefabs (Lobby).");
                Object.Destroy(instance);
                return false;
            }

            if (!instance.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                networkEntity = instance.AddComponent<RtsUtsNetworkEntity>();
            }

            networkEntity.ServerConfigure(connection != null ? connection.connectionId : -1, owner);

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

        /// <summary>
        /// Mục tiêu: Tránh Mirror từ chối spawn vì thiếu prefab trong danh sách Lobby.
        /// Cách hoạt động: Thêm civil central + worker vào spawnPrefabs của NetworkManager nếu chưa có.
        /// </summary>
        public static void EnsureSpawnPrefabsRegistered(RtsUtsGameSceneSetup setup)
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null || setup == null)
            {
                return;
            }

            RegisterPrefab(manager, setup.civilCentralPrefab);
            RegisterPrefab(manager, setup.startingWorkerPrefab);
        }

        static void RegisterPrefab(NetworkManager manager, GameObject prefab)
        {
            if (prefab == null || manager.spawnPrefabs.Contains(prefab))
            {
                return;
            }

            manager.spawnPrefabs.Add(prefab);
            Debug.Log($"[RtsUtsServerSpawnHandler] Đã thêm {prefab.name} vào NetworkManager.spawnPrefabs.");
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

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
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

                NetworkIdentity candidateId = candidate.GetComponent<NetworkIdentity>();
                if (candidateId != null && candidateId.assetId == identity.assetId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: MP PvP phase 1 không chạy AI offline trên scene trận.
        /// Cách hoạt động: Disable mọi <see cref="AIController"/> trong scene hiện tại.
        /// </summary>
        public static void DisableAiControllersInScene()
        {
            AIController[] controllers = Object.FindObjectsByType<AIController>(FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    controllers[i].enabled = false;
                }
            }
        }
    }
}
