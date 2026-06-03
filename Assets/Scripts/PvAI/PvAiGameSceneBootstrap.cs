using GameDevTV.RTS.Game.Startup;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Khi Play map offline — chạy spawn PvE (không chạy khi Mirror active).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class PvAiGameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] PvAiGameSceneSetup sceneSetup;

        void Awake()
        {
            if (sceneSetup == null)
            {
                sceneSetup = GetComponent<PvAiGameSceneSetup>();
            }

            if (!ShouldRunOfflineSpawn())
            {
                return;
            }

            PvAiOfflineSpawnCoordinator.TryEnsureHumanBaseSpawned();
        }

        void Start()
        {
            if (!ShouldRunOfflineSpawn())
            {
                return;
            }

            PvAiOfflineSpawnCoordinator.TryEnsureHumanBaseSpawned();
            PvAiOfflineAiCoordinator.TryEnableForOfflinePvE();
            PregameAiDifficultyApplyService.TryApply();
        }

        bool ShouldRunOfflineSpawn()
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

            if (sceneSetup != null && sceneSetup.gameObject.scene != scene)
            {
                return false;
            }

            return true;
        }
    }
}
