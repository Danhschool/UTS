using System.Collections;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Phase A — Loading scene: async load file (offline) hoặc chờ host chuyển scene (MP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayLoadingSceneController : MonoBehaviour
    {
        [SerializeField] float minLoadingSeconds = 2f;
        [SerializeField] GameplayLoadingSceneView view;

        static GameplayLoadingSceneController activeController;

        void Awake()
        {
            activeController = this;
            view ??= GetComponentInChildren<GameplayLoadingSceneView>(true);
            view ??= GameplayLoadingSceneView.CreateRuntime();
        }

        void Start()
        {
            if (!GameplaySceneLoader.HasPendingTarget)
            {
                Debug.LogWarning(
                    "[GameplayLoadingSceneController] Không có scene đích. Quay MainMenu hoặc gọi GameplaySceneLoader.RequestLoad.");
                return;
            }

            GameplaySceneLoaderHost host = GameplaySceneLoaderHost.Ensure();
            EnsureViewOnHost(host.transform);
            host.Run(RunLoadingPhaseA());
        }

        void EnsureViewOnHost(Transform hostTransform)
        {
            if (view != null && view.transform.parent == hostTransform)
            {
                return;
            }

            view = hostTransform.GetComponentInChildren<GameplayLoadingSceneView>(true);
            if (view == null)
            {
                view = GameplayLoadingSceneView.CreateRuntime(hostTransform);
            }
        }

        void OnDestroy()
        {
            if (activeController == this)
            {
                activeController = null;
            }
        }

        /// <summary>
        /// Mục tiêu: Gọi lại khi đã ở Loading scene và vừa set pending target.
        /// </summary>
        public static void StartPendingLoadOnActiveScene()
        {
            if (activeController != null)
            {
                GameplaySceneLoaderHost.Ensure().Run(activeController.RunLoadingPhaseA());
            }
        }

        IEnumerator RunLoadingPhaseA()
        {
            minLoadingSeconds = Mathf.Max(0f, minLoadingSeconds);
            float started = Time.unscaledTime;

            view?.SetStatus("Đang tải scene…");
            view?.SetProgress01(0f);

            if (GameplaySceneLoader.PendingUseNetworkHandoff)
            {
                yield return RunNetworkHandoff(started);
                yield break;
            }

            yield return RunOfflineAsyncLoad(started);
        }

        IEnumerator RunOfflineAsyncLoad(float startedUnscaled)
        {
            string target = GameplaySceneLoader.PendingTargetScene;
            AsyncOperation operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[GameplayLoadingSceneController] LoadSceneAsync thất bại: {target}");
                yield break;
            }

            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                float fileProgress = Mathf.Clamp01(operation.progress / 0.9f);
                UpdatePhaseAUi(startedUnscaled, fileProgress);
                yield return null;
            }

            while (Time.unscaledTime - startedUnscaled < minLoadingSeconds)
            {
                UpdatePhaseAUi(startedUnscaled, 1f);
                yield return null;
            }

            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                UpdatePhaseAUi(startedUnscaled, 1f);
                yield return null;
            }

            GameplaySceneLoader.MarkGameplayEntered();
            GameplayInGameReadyController.BeginFromLoaderHost();
        }

        IEnumerator RunNetworkHandoff(float startedUnscaled)
        {
            view?.SetStatus("Đang chuẩn bị trận đấu…");

            while (Time.unscaledTime - startedUnscaled < minLoadingSeconds)
            {
                float timeProgress = minLoadingSeconds > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - startedUnscaled) / minLoadingSeconds)
                    : 1f;
                view?.SetProgress01(timeProgress * GameplaySceneLoader.PhaseAProgressCap);
                yield return null;
            }

            if (NetworkServer.active)
            {
                RtsNetworkManager manager = NetworkManager.singleton as RtsNetworkManager;
                string target = GameplaySceneLoader.PendingTargetScene;
                if (manager != null && !string.IsNullOrWhiteSpace(target))
                {
                    manager.ServerChangeSceneFromLoading(target);
                }
                else
                {
                    Debug.LogError("[GameplayLoadingSceneController] Không tìm thấy RtsNetworkManager để chuyển scene game.");
                }

                while (SceneManager.GetActiveScene().name == GameplaySceneLoader.LoadingSceneName)
                {
                    yield return null;
                }
            }
            else
            {
                while (SceneManager.GetActiveScene().name == GameplaySceneLoader.LoadingSceneName)
                {
                    UpdatePhaseAUi(startedUnscaled, 1f);
                    yield return null;
                }
            }

            GameplaySceneLoader.MarkGameplayEntered();
            GameplayInGameReadyController.BeginFromLoaderHost();
        }

        void UpdatePhaseAUi(float startedUnscaled, float fileProgress01)
        {
            float cap = GameplaySceneLoader.PhaseAProgressCap;
            float timeProgress = minLoadingSeconds > 0f
                ? Mathf.Clamp01((Time.unscaledTime - startedUnscaled) / minLoadingSeconds)
                : 1f;
            float combined = Mathf.Min(fileProgress01, timeProgress) * cap;
            view?.SetProgress01(combined);
        }
    }
}
