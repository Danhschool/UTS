using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Khi server vào scene trận — spawn UTS hoặc fallback capsule MVP cho mỗi connection.
    /// </summary>
    public static class RtsMatchServerSpawnOrchestrator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsServerGameplayNotifier.OnMatchSceneLoaded = HandleMatchSceneLoaded;
        }

        static void HandleMatchSceneLoaded()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            RtsUtsGameSceneSetup utsSetup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>();
            RtsGameSceneSetup legacySetup = Object.FindFirstObjectByType<RtsGameSceneSetup>();
            Transform[] spawnPoints = utsSetup != null ? utsSetup.teamSpawnPoints : legacySetup?.teamSpawnPoints;

            if (spawnPoints == null || spawnPoints.Length < 2)
            {
                Debug.LogError("[RtsMatchServerSpawnOrchestrator] Thiếu spawn points (RtsUtsGameSceneSetup hoặc RtsGameSceneSetup).");
                return;
            }

            bool useUts = utsSetup != null
                          && (utsSetup.civilCentralPrefab != null || utsSetup.startingWorkerPrefab != null);

            if (useUts && utsSetup.disableAiControllersOnLoad)
            {
                RtsUtsServerSpawnHandler.DisableAiControllersInScene();
            }

            foreach (var kvp in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = kvp.Value;
                if (connection == null || connection.identity == null)
                {
                    continue;
                }

                RtsLobbyPlayer lobbyPlayer = connection.identity.GetComponent<RtsLobbyPlayer>();
                if (lobbyPlayer == null)
                {
                    continue;
                }

                int team = Mathf.Clamp(lobbyPlayer.PlayerTeamIndex, 0, 1);
                Vector3 spawn = spawnPoints[team].position;

                if (useUts)
                {
                    RtsServerGameplayNotifier.NotifySpawnTeam(
                        new RtsServerSpawnRequest(connection.connectionId, team, connection.identity.netId, spawn));
                    continue;
                }

                SpawnLegacyCapsule(connection, team, spawn);
            }
        }

        static void SpawnLegacyCapsule(NetworkConnectionToClient connection, int team, Vector3 spawn)
        {
            RtsNetworkManager networkManager = Object.FindFirstObjectByType<RtsNetworkManager>();
            if (networkManager == null || networkManager.unitPrefab == null)
            {
                return;
            }

            GameObject instance = Object.Instantiate(networkManager.unitPrefab, spawn, Quaternion.identity);
            RtsUnit unit = instance.GetComponent<RtsUnit>();
            if (unit != null)
            {
                unit.ServerAssignOwner(connection.connectionId, team, connection.identity.netId);
            }

            NetworkServer.Spawn(instance);
        }
    }
}
