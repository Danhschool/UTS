using System;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// DIP: Netplay assembly báo server cần spawn gameplay UTS; handler đăng ký từ game assembly.
    /// </summary>
    public static class RtsServerGameplayNotifier
    {
        public static Action OnMatchSceneLoaded;

        public static Action<RtsServerSpawnRequest> OnSpawnTeamGameplay;

        /// <summary>Slot 0/1 — game assembly xử lý forfeit + reset spawn.</summary>
        public static Action<int> OnPlayerDisconnectedForfeit;

        public static Action OnClientDisconnectedCleanup;

        public static bool MatchSpawnCompleted { get; set; }

        public static void ResetMatchSpawnState() => MatchSpawnCompleted = false;

        public static void NotifyMatchSceneLoaded() => OnMatchSceneLoaded?.Invoke();

        public static void NotifySpawnTeam(in RtsServerSpawnRequest request) =>
            OnSpawnTeamGameplay?.Invoke(request);

        public static void NotifyPlayerDisconnectedForfeit(int teamSlot) =>
            OnPlayerDisconnectedForfeit?.Invoke(teamSlot);

        public static void NotifyClientDisconnectedCleanup() =>
            OnClientDisconnectedCleanup?.Invoke();
    }

    public readonly struct RtsServerSpawnRequest
    {
        public readonly int ConnectionId;
        public readonly int TeamIndex;
        public readonly uint PlayerNetId;
        public readonly Vector3 SpawnPosition;

        public RtsServerSpawnRequest(int connectionId, int teamIndex, uint playerNetId, Vector3 spawnPosition)
        {
            ConnectionId = connectionId;
            TeamIndex = teamIndex;
            PlayerNetId = playerNetId;
            SpawnPosition = spawnPosition;
        }
    }
}
