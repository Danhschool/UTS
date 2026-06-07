using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.DebugCheats
{
    /// <summary>
    /// SRP: Phím F6 (Editor / Development Build) — chiến thắng ngay bằng cách phá CC địch.
    /// </summary>
    public sealed class MatchDebugInstantWinHotkey : MonoBehaviour
    {
        [SerializeField] Key instantWinKey = Key.F6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#endif
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryEnsureForActiveScene();
        }

        void Awake()
        {
            instantWinKey = InputSystemKeyboardUtility.CoerceKeyboardKey(instantWinKey, Key.F6);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            instantWinKey = InputSystemKeyboardUtility.CoerceKeyboardKey(instantWinKey, Key.F6);
        }
#endif

        void Reset()
        {
            instantWinKey = Key.F6;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!InputSystemKeyboardUtility.WasPressedThisFrame(instantWinKey))
            {
                return;
            }

            if (MatchDebugInstantWinService.TryDestroyEnemyCivilCentral(out string reason))
            {
                UnityEngine.Debug.Log($"[MatchDebugInstantWin] F6 — đã gây sát thương CC địch.");
                return;
            }

            UnityEngine.Debug.LogWarning($"[MatchDebugInstantWin] F6 — {reason}");
#endif
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryEnsureForActiveScene();
        }

        static void TryEnsureForActiveScene()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            EnsureListenerExists();
        }

        static void EnsureListenerExists()
        {
            if (Object.FindFirstObjectByType<MatchDebugInstantWinHotkey>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var host = new GameObject(nameof(MatchDebugInstantWinHotkey));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<MatchDebugInstantWinHotkey>();
        }
    }
}
