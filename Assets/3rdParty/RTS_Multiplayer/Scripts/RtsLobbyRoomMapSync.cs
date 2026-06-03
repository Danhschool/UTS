using System;
using Mirror;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Truy cập map lobby từ host player (player prefab đã đăng ký Mirror — không spawn runtime).
    /// </summary>
    public static class RtsLobbyRoomMapSync
    {
        public static event Action<int, string> MapSelectionChanged;

        public static bool TryGetHostLobbyPlayer(out RtsLobbyPlayer hostPlayer)
        {
            hostPlayer = null;

            if (NetworkServer.active)
            {
                foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                {
                    if (connection?.identity == null)
                    {
                        continue;
                    }

                    RtsLobbyPlayer player = connection.identity.GetComponent<RtsLobbyPlayer>();
                    if (player != null && player.PlayerTeamIndex == 0)
                    {
                        hostPlayer = player;
                        return true;
                    }
                }

                return false;
            }

            if (!NetworkClient.active)
            {
                return false;
            }

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity == null)
                {
                    continue;
                }

                RtsLobbyPlayer player = identity.GetComponent<RtsLobbyPlayer>();
                if (player != null && player.PlayerTeamIndex == 0)
                {
                    hostPlayer = player;
                    return true;
                }
            }

            return false;
        }

        internal static void RaiseMapSelectionChanged(int mapIndex, string gameplayScene)
        {
            MapSelectionChanged?.Invoke(mapIndex, gameplayScene);
        }
    }

    public static class PregameGameplaySceneFallback
    {
        public const string DefaultScene = "Game 1";
    }
}
