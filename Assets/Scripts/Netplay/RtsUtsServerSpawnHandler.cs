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
                if (TrySpawnCivilCentral(setup.civilCentralPrefab, spawn, connection, owner))
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
            return RtsUtsServerEntityFactory.TrySpawnGameplayEntity(
                prefab,
                position,
                Quaternion.identity,
                owner,
                connection,
                out _);
        }

        /// <summary>
        /// Mục tiêu: CC đầu trận luôn Completed + sync trước frame đầu của client.
        /// </summary>
        static bool TrySpawnCivilCentral(
            GameObject prefab,
            Vector3 position,
            NetworkConnectionToClient connection,
            Owner owner)
        {
            if (!RtsUtsServerEntityFactory.TrySpawnGameplayEntity(
                    prefab,
                    position,
                    Quaternion.identity,
                    owner,
                    connection,
                    out GameObject instance))
            {
                return false;
            }

            if (instance.TryGetComponent(out BaseBuilding building))
            {
                building.EnsureCivilCentralMatchStartReady();
            }

            return true;
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
