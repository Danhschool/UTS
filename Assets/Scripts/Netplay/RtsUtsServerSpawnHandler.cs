using GameDevTV.RTS.AI;
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

            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>();
            if (setup == null)
            {
                return;
            }

            Owner owner = OwnerTeamMapping.FromTeamIndex(request.TeamIndex);
            Vector3 spawn = request.SpawnPosition;

            if (setup.civilCentralPrefab != null)
            {
                SpawnEntity(setup.civilCentralPrefab, spawn, request.ConnectionId, owner);
            }

            if (setup.startingWorkerPrefab != null)
            {
                Vector3 workerPos = spawn + new Vector3(2f, 0f, 2f);
                SpawnEntity(setup.startingWorkerPrefab, workerPos, request.ConnectionId, owner);
            }
        }

        static void SpawnEntity(GameObject prefab, Vector3 position, int connectionId, Owner owner)
        {
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            if (!instance.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"[RtsUtsServerSpawnHandler] Prefab {prefab.name} thiếu NetworkIdentity.");
                Object.Destroy(instance);
                return;
            }

            if (!instance.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                networkEntity = instance.AddComponent<RtsUtsNetworkEntity>();
            }

            networkEntity.ServerConfigure(connectionId, owner);
            NetworkServer.Spawn(instance);
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
