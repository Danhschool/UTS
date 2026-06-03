using System.Collections.Generic;
using GameDevTV.RTS.Game.Startup;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Khi server vào scene trận — spawn Civil Central + worker UTS cho mỗi connection.
    /// </summary>
    public static class RtsMatchServerSpawnOrchestrator
    {
        const int RequiredHumanConnections = 2;

        static readonly HashSet<int> SpawnedConnectionIds = new();
        static string _spawnSessionSceneName = string.Empty;

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

            BeginSpawnSessionForActiveScene();
            RtsMatchServerSpawnRunner.EnsureScheduled();
            if (TryMarkMatchSpawnComplete())
            {
                RtsServerGameplayNotifier.MatchSpawnCompleted = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Spawn CC + worker khi đủ 2 connection có lobby identity; không đánh dấu xong khi mới spawn 1 phe.
        /// Cách hoạt động: Theo dõi connection đã spawn; retry an toàn; chỉ hoàn tất khi đủ readyCount.
        /// </summary>
        public static int TrySpawnMatchGameplay()
        {
            if (!NetworkServer.active || RtsServerGameplayNotifier.MatchSpawnCompleted)
            {
                return 0;
            }

            BeginSpawnSessionForActiveScene();

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

            int teamsWithSpawn = 0;

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

                if (SpawnedConnectionIds.Contains(connection.connectionId))
                {
                    teamsWithSpawn++;
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
                bool spawnedThisPass;

                if (useUts)
                {
                    RtsServerGameplayNotifier.NotifySpawnTeam(
                        new RtsServerSpawnRequest(connection.connectionId, team, connection.identity.netId, spawn));
                    spawnedThisPass = true;
                }
                else
                {
                    spawnedThisPass = SpawnLegacyCapsule(connection, team, spawn);
                }

                if (!spawnedThisPass)
                {
                    continue;
                }

                SpawnedConnectionIds.Add(connection.connectionId);
                teamsWithSpawn++;
            }

            if (teamsWithSpawn >= readyCount && readyCount >= RequiredHumanConnections)
            {
                Debug.Log(
                    $"[RtsMatchServerSpawnOrchestrator] Đã spawn gameplay cho {teamsWithSpawn}/{readyCount} người (scene={SceneManager.GetActiveScene().name}).");
                return teamsWithSpawn;
            }

            return 0;
        }

        /// <summary>
        /// Mục tiêu: Chỉ đánh dấu trận spawn xong khi đủ 2 phe — tránh P2 không có unit/fog.
        /// </summary>
        public static bool TryMarkMatchSpawnComplete()
        {
            int teamsWithSpawn = TrySpawnMatchGameplay();
            return teamsWithSpawn >= RequiredHumanConnections;
        }

        static void BeginSpawnSessionForActiveScene()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                _spawnSessionSceneName = string.Empty;
                SpawnedConnectionIds.Clear();
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (_spawnSessionSceneName == sceneName)
            {
                return;
            }

            _spawnSessionSceneName = sceneName;
            SpawnedConnectionIds.Clear();
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

        /// <summary>
        /// Mục tiêu: Log lỗi spawn “lâu lâu” — connection chưa identity, thiếu setup, prefab chưa đăng ký.
        /// </summary>
        public static void LogSpawnFailureDiagnostics()
        {
            int connectionCount = NetworkServer.connections.Count;
            AreHumanConnectionsReady(out int readyCount);

            RtsUtsGameSceneSetup utsSetup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            bool hasSetup = utsSetup != null;
            bool hasPoints = utsSetup != null
                             && utsSetup.teamSpawnPoints != null
                             && utsSetup.teamSpawnPoints.Length >= 2;
            bool hasCc = utsSetup != null && utsSetup.civilCentralPrefab != null;
            bool hasWorker = utsSetup != null && utsSetup.startingWorkerPrefab != null;

            Debug.LogError(
                $"[RtsMatchServerSpawnOrchestrator] Spawn thất bại — scene='{SceneManager.GetActiveScene().name}', " +
                $"connections={connectionCount}, lobbyReady={readyCount}/{RequiredHumanConnections}, " +
                $"spawnedIds={SpawnedConnectionIds.Count}, hasSetup={hasSetup}, spawnPointsOk={hasPoints}, " +
                $"ccPrefab={hasCc}, workerPrefab={hasWorker}. " +
                "Kiểm tra: cả 2 client Ready trước Start; map có RtsUtsGameSceneSetup; prefab có NetworkIdentity + trong Spawn Prefabs.");

            foreach (var kvp in NetworkServer.connections)
            {
                NetworkConnectionToClient connection = kvp.Value;
                if (connection == null)
                {
                    continue;
                }

                bool hasIdentity = connection.identity != null;
                int team = -1;
                if (hasIdentity && connection.identity.TryGetComponent(out RtsLobbyPlayer lobbyPlayer))
                {
                    team = lobbyPlayer.PlayerTeamIndex;
                }

                Debug.LogWarning(
                    $"[RtsMatchServerSpawnOrchestrator] conn={connection.connectionId}, identity={hasIdentity}, team={team}, alreadySpawned={SpawnedConnectionIds.Contains(connection.connectionId)}");
            }
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
