using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Khi Play Game 1 offline — chạy spawn PvE một lần (không chạy khi Mirror active).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PvAiGameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] string targetSceneName = PvAiSceneValidator.Game1SceneName;
        [SerializeField] PvAiGameSceneSetup sceneSetup;

        static bool s_spawnedThisSession;

        void Awake()
        {
            if (sceneSetup == null)
            {
                sceneSetup = GetComponent<PvAiGameSceneSetup>();
            }
        }

        void Start()
        {
            if (s_spawnedThisSession || !ShouldRunOfflineSpawn())
            {
                return;
            }

            if (sceneSetup == null)
            {
                sceneSetup = FindFirstObjectByType<PvAiGameSceneSetup>();
            }

            if (sceneSetup == null)
            {
                Debug.LogWarning(
                    "[PvAiGameSceneBootstrap] Không có PvAiGameSceneSetup — bỏ qua spawn PvE. "
                    + "Chạy menu ProjectRTS/PvAI/Setup Game 1 spawn (long-term).");
                return;
            }

            PvAiOfflineSpawnService.SpawnMatch(sceneSetup);
            s_spawnedThisSession = true;
        }

        bool ShouldRunOfflineSpawn()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.name.Contains(targetSceneName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => s_spawnedThisSession = false;
    }
}
