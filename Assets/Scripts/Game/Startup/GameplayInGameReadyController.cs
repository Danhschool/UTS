using GameDevTV.RTS.Audio;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.PvAI;
using UnityEngine;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Phase B — trong scene game: chờ owner + Civil Central, bar 85%→100%, mở input/audio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayInGameReadyController : MonoBehaviour
    {
        [SerializeField] float minReadySeconds = 3f;

        GameplayLoadingSceneView view;
        float startedUnscaled;
        bool isRunning;

        public static void BeginFromLoaderHost()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            GameplaySceneLoaderHost host = GameplaySceneLoaderHost.Ensure();
            if (host.GetComponent<GameplayInGameReadyController>() != null)
            {
                return;
            }

            host.gameObject.AddComponent<GameplayInGameReadyController>();
        }

        void Start()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                Destroy(this);
                return;
            }

            GameplaySceneLoaderHost host = GameplaySceneLoaderHost.Ensure();
            view = host.GetComponentInChildren<GameplayLoadingSceneView>(true);
            view ??= GameplayLoadingSceneView.CreateRuntime(host.transform);
            BeginReadyWait();
        }

        void Update()
        {
            if (!isRunning)
            {
                return;
            }

            TickReadyPhase();
        }

        void BeginReadyWait()
        {
            isRunning = true;
            startedUnscaled = Time.unscaledTime;
            minReadySeconds = Mathf.Max(0f, minReadySeconds);
            GameplayStartupGate.Lock();
            AudioAccess.TryStopMusic();
            view?.SetStatus("Đang khởi tạo trận đấu…");
            view?.SetProgress01(GameplaySceneLoader.PhaseAProgressCap);
        }

        void TickReadyPhase()
        {
            float elapsed = Time.unscaledTime - startedUnscaled;
            float timeProgress = minReadySeconds > 0f
                ? Mathf.Clamp01(elapsed / minReadySeconds)
                : 1f;

            bool contentReady = GameplayStartupReadiness.TryEvaluate(
                startedUnscaled,
                out float contentProgress,
                out string status,
                out bool timedOut);

            float phaseStart = GameplaySceneLoader.PhaseAProgressCap;
            float phaseSpan = 1f - phaseStart;
            float contentMapped = contentReady ? 1f : Mathf.Lerp(phaseStart, phaseStart + phaseSpan * 0.75f, contentProgress);
            float bar = contentReady
                ? phaseStart + phaseSpan * timeProgress
                : Mathf.Min(phaseStart + phaseSpan * timeProgress, contentMapped);

            view?.SetStatus(status);
            view?.SetProgress01(bar);

            if (!contentReady)
            {
                return;
            }

            if (elapsed < minReadySeconds)
            {
                return;
            }

            if (timedOut)
            {
                Debug.LogWarning($"[GameplayInGameReadyController] Timeout — mở game. {status}");
            }

            FinishReadyPhase();
        }

        void FinishReadyPhase()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            view?.SetProgress01(1f);

            if (view != null)
            {
                Destroy(view.gameObject);
                view = null;
            }

            GameplayStartupGate.Unlock();
            PregameAiDifficultyApplyService.TryApply();
            AudioAccess.TryStartGameplayMusic();
            GameplaySceneLoader.CompleteLoadFlow();

            FindFirstObjectByType<MpPlayerPresentationDirector>(FindObjectsInactive.Include)
                ?.RefreshFromLocalOwner();
            LocalHumanCameraSpawnFocus.RequestRefocusForLocalHuman();

            Destroy(this);
            CleanupLoaderHostIfIdle();
        }

        static void CleanupLoaderHostIfIdle()
        {
            GameplaySceneLoaderHost host = Object.FindFirstObjectByType<GameplaySceneLoaderHost>();
            if (host == null)
            {
                return;
            }

            if (host.GetComponents<MonoBehaviour>().Length > 1)
            {
                return;
            }

            Object.Destroy(host.gameObject);
        }
    }
}
