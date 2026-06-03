using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Spawn PvE offline khi bootstrap hoặc loading bar cần căn cứ Player1.
    /// </summary>
    public static class PvAiOfflineSpawnCoordinator
    {
        /// <summary>
        /// Mục tiêu: Đảm bảo có Civil Central Player1 trước khi mở gameplay.
        /// Cách hoạt động: Chuẩn bị owner offline → SpawnMatch nếu thiếu căn cứ.
        /// </summary>
        public static bool TryEnsureHumanBaseSpawned()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !GameplayStartupScenes.IsGameplayScene(scene))
            {
                return false;
            }

            PvAiOfflineSessionPrep.PrepareLocalHumanOwner();

            if (PvAiOfflineSessionPrep.HasHumanCivilCentral())
            {
                PvAiOfflineSessionPrep.MarkSpawnCompletedForCurrentLoad();
                return true;
            }

            if (!PvAiOfflineSessionPrep.ShouldSpawnForCurrentLoad())
            {
                return false;
            }

            PvAiGameSceneSetup setup = Object.FindFirstObjectByType<PvAiGameSceneSetup>(FindObjectsInactive.Include);
            if (setup == null)
            {
                Debug.LogWarning(
                    "[PvAiOfflineSpawnCoordinator] Thiếu PvAiGameSceneSetup — chạy ProjectRTS/Gameplay/★ Prepare Open Scene As Gameplay Map.");
                return false;
            }

            PvAiOfflineSpawnService.SpawnMatch(setup);

            if (PvAiOfflineSessionPrep.HasHumanCivilCentral())
            {
                PvAiOfflineSessionPrep.MarkSpawnCompletedForCurrentLoad();
                return true;
            }

            Debug.LogError(
                "[PvAiOfflineSpawnCoordinator] Spawn PvE xong nhưng không thấy Civil Central Player1. "
                + "Kiểm tra spawn points + civil_central prefab trên GameplayMapCore.");
            return false;
        }
    }
}
