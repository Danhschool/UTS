using GameDevTV.RTS.AI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Bật AI bot cho PvE offline — không tắt vĩnh viễn khi Prepare map cho MP.
    /// </summary>
    public static class PvAiOfflineAiCoordinator
    {
        static int s_enabledForLoadGeneration = -1;

        /// <summary>
        /// Mục tiêu: AI2 hoạt động trong PvE sau khi menu Prepare từng disable AIController.
        /// Cách hoạt động: Bật mọi AIController phe AI khi offline; MP vẫn tắt qua RtsUtsServerSpawnHandler.
        /// </summary>
        public static void TryEnableForOfflinePvE()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            int generation = PvAiOfflineSessionPrep.CurrentLoadGeneration;
            if (s_enabledForLoadGeneration == generation)
            {
                return;
            }

            AIController[] controllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int enabledCount = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                AIController controller = controllers[i];
                if (controller == null || HumanFogVisionUtility.IsHumanPlayer(controller.AiOwner))
                {
                    continue;
                }

                controller.enabled = true;
                enabledCount++;
            }

            s_enabledForLoadGeneration = generation;

            if (enabledCount == 0)
            {
                Debug.LogWarning("[PvAiOfflineAi] Không có AIController phe bot trong scene — thêm AI Manager + AIController.");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => s_enabledForLoadGeneration = -1;
    }
}
