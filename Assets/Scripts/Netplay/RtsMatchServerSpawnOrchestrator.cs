using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Khi server vào scene trận — spawn Civil Central + worker UTS cho mỗi connection.
    /// </summary>
    public static class RtsMatchServerSpawnOrchestrator
    {
        const int RequiredHumanConnections = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsServerGameplayNotifier.OnMatchSceneLoaded = RequestSpawn;
        }

        public static void RequestSpawn()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            RtsMatchServerSpawnRunner.EnsureScheduled();
            int spawnedTeams = TrySpawnMatchGameplay();
            if (spawnedTeams > 0)
            {
                RtsServerGameplayNotifier.MatchSpawnCompleted = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Spawn CC + worker khi lobby player đã có identity trong scene trận.
        /// Cách hoạt động: Đọc spawn points; với mỗi connection có RtsLobbyPlayer gọi NotifySpawnTeam.
        /// </summary>
        public static int TrySpawnMatchGameplay()
        {
            if (!NetworkServer.active || RtsServerGameplayNotifier.MatchSpawnCompleted)
            {
                return 0;
            }

            if (!AreHumanConnectionsReady(out int readyCount))
            {
                return 0;
            }

            RtsUtsGameSceneSetup utsSetup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            RtsGameSceneSetup legacySetup = Object.FindFirstObjectByType<RtsGameSceneSetup>(FindObjectsInactive.Include);
            Transform[] spawnPoints = utsSetup != null ? utsSetup.teamSpawnPoints : legacySetup?.teamSpawnPoints;

            if (spawnPoints == null || spawnPoints.Length < 2)
            {
                Debug.LogError("[RtsMatchServerSpawnOrchestrator] Thiếu spawn points (RtsUtsGameSceneSetup / RtsGameSceneSetup).");
                return 0;
            }

            bool useUts = utsSetup != null
                            && (utsSetup.civilCentralPrefab != null || utsSetup.startingWorkerPrefab != null);

            if (!useUts)
            {
                Debug.LogWarning("[RtsMatchServerSpawnOrchestrator] Chưa gán civilCentralPrefab / startingWorkerPrefab — dùng capsule MVP.");
            }
            else
            {
                RtsUtsServerSpawnHandler.EnsureSpawnPrefabsRegistered(utsSetup);
                if (utsSetup.disableAiControllersOnLoad)
                {
                    RtsUtsServerSpawnHandler.DisableAiControllersInScene();
                }
            }

            int spawnedTeams = 0;

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
                Transform spawnTransform = spawnPoints[team];
                if (spawnTransform == null)
                {
                    Debug.LogError($"[RtsMatchServerSpawnOrchestrator] Spawn point team {team} null.");
                    continue;
                }

                Vector3 spawn = spawnTransform.position;

                if (useUts)
                {
                    RtsServerGameplayNotifier.NotifySpawnTeam(
                        new RtsServerSpawnRequest(connection.connectionId, team, connection.identity.netId, spawn));
                    spawnedTeams++;
                    continue;
                }

                if (SpawnLegacyCapsule(connection, team, spawn))
                {
                    spawnedTeams++;
                }
            }

            if (spawnedTeams > 0)
            {
                Debug.Log($"[RtsMatchServerSpawnOrchestrator] Đã spawn gameplay cho {spawnedTeams}/{readyCount} người.");
            }

            return spawnedTeams;
        }

        /// <summary>
        /// Mục tiêu: Chỉ spawn khi đủ 2 người và mỗi connection đã có lobby player object.
        /// Cách hoạt động: Đếm connection có identity + RtsLobbyPlayer.
        /// </summary>
        public static bool AreHumanConnectionsReady(out int readyCount)
        {
            readyCount = 0;

            if (NetworkServer.connections.Count < RequiredHumanConnections)
            {
                return false;
            }

            foreach (var kvp in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = kvp.Value;
                if (connection == null || connection.identity == null)
                {
                    continue;
                }

                if (connection.identity.GetComponent<RtsLobbyPlayer>() != null)
                {
                    readyCount++;
                }
            }

            return readyCount >= RequiredHumanConnections;
        }

        static bool SpawnLegacyCapsule(NetworkConnectionToClient connection, int team, Vector3 spawn)
        {
            RtsNetworkManager networkManager = Object.FindFirstObjectByType<RtsNetworkManager>();
            if (networkManager == null || networkManager.unitPrefab == null)
            {
                return false;
            }

            GameObject instance = Object.Instantiate(networkManager.unitPrefab, spawn, Quaternion.identity);
            RtsUnit unit = instance.GetComponent<RtsUnit>();
            if (unit != null)
            {
                unit.ServerAssignOwner(connection.connectionId, team, connection.identity.netId);
            }

            NetworkServer.Spawn(instance, connection);
            return true;
        }
    }
}
