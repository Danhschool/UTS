using GameDevTV.RTS.PvAI;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Kiểm tra client/local đã sẵn sàng chơi (owner + Civil Central).
    /// </summary>
    public static class GameplayStartupReadiness
    {
        const float TimeoutSeconds = 30f;

        /// <summary>
        /// Mục tiêu: Trả về sẵn sàng + tiến độ nội dung (0–1) cho thanh fill.
        /// Cách hoạt động: Các bước init → CC local; timeout = sẵn sàng cưỡng bức.
        /// </summary>
        public static bool TryEvaluate(
            float startedUnscaledTime,
            out float contentProgress,
            out string statusMessage,
            out bool timedOut)
        {
            timedOut = Time.unscaledTime - startedUnscaledTime >= TimeoutSeconds;

            if (NetworkClient.active && !NetworkClient.isConnected)
            {
                contentProgress = 0.12f;
                statusMessage = "Đang kết nối…";
                return false;
            }

            if (NetworkClient.active)
            {
                MpLocalOwnerSceneSync.RefreshAfterGameSceneLoad();
            }

            if (!MpLocalOwnerSceneSync.EnsureLocalOwnerInitialized(out Owner localOwner))
            {
                contentProgress = NetworkClient.active ? 0.35f : 0.28f;
                statusMessage = NetworkClient.active ? "Đang đồng bộ phe…" : "Đang gán người chơi…";
                return timedOut;
            }

            if (!HumanFogVisionUtility.IsHumanPlayer(localOwner))
            {
                contentProgress = 0.45f;
                statusMessage = "Đang chờ phe người chơi…";
                return timedOut;
            }

            if (!NetworkClient.active && !NetworkServer.active)
            {
                PvAiOfflineSpawnCoordinator.TryEnsureHumanBaseSpawned();
            }

            if (HasLocalCivilCentral(localOwner))
            {
                contentProgress = 1f;
                statusMessage = "Sẵn sàng";
                return true;
            }

            contentProgress = 0.72f;
            statusMessage = NetworkClient.active ? "Đang spawn căn cứ…" : "Đang tải căn cứ…";
            return timedOut;
        }

        static bool HasLocalCivilCentral(Owner localOwner)
        {
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null
                    || building.Owner != localOwner
                    || !CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
