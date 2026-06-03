using System.Collections;
using GameDevTV.RTS.Game;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Retry spawn UTS sau khi Mirror chuyển scene — identity lobby đôi khi chưa sẵn sàng ngay OnServerSceneChanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsMatchServerSpawnRunner : MonoBehaviour
    {
        const int MaxFrames = 120;

        static RtsMatchServerSpawnRunner _instance;

        public static void EnsureScheduled()
        {
            if (!NetworkServer.active || RtsServerGameplayNotifier.MatchSpawnCompleted)
            {
                return;
            }

            if (_instance != null)
            {
                _instance.StartRetryIfNeeded();
                return;
            }

            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>(FindObjectsInactive.Include);
            GameObject host = setup != null ? setup.gameObject : new GameObject(nameof(RtsMatchServerSpawnRunner));
            _instance = host.GetComponent<RtsMatchServerSpawnRunner>();
            if (_instance == null)
            {
                _instance = host.AddComponent<RtsMatchServerSpawnRunner>();
            }

            _instance.StartRetryIfNeeded();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        void StartRetryIfNeeded()
        {
            StopAllCoroutines();
            if (!NetworkServer.active || RtsServerGameplayNotifier.MatchSpawnCompleted)
            {
                return;
            }

            StartCoroutine(RetrySpawnUntilReady());
        }

        IEnumerator RetrySpawnUntilReady()
        {
            for (int frame = 0; frame < MaxFrames; frame++)
            {
                if (!NetworkServer.active)
                {
                    yield break;
                }

                if (RtsServerGameplayNotifier.MatchSpawnCompleted)
                {
                    yield break;
                }

                int spawnedTeams = RtsMatchServerSpawnOrchestrator.TrySpawnMatchGameplay();
            if (spawnedTeams > 0)
            {
                RtsServerGameplayNotifier.MatchSpawnCompleted = true;
                GameMatchOverlayStateSync.EnsureServerInstance();
                yield break;
            }

                yield return null;
            }

            if (!RtsServerGameplayNotifier.MatchSpawnCompleted)
            {
                Debug.LogError(
                    "[RtsMatchServerSpawnRunner] Không spawn được CC/worker sau khi vào RtsNet_Game. " +
                    "Kiểm tra: 2 client Ready, RtsUtsGameSceneSetup spawn points + prefab, Spawn Prefabs trên NetworkManager.");
            }
        }
    }
}
