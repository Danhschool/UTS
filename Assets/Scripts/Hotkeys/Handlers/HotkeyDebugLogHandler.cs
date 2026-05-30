using UnityEngine;

namespace GameDevTV.RTS.Hotkeys.Handlers
{
    /// <summary>
    /// Debug: log mọi hotkey được kích hoạt (tắt trên build release).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotkeyDebugLogHandler : HotkeyHandlerBase
    {
        [SerializeField] HotkeyId id;
        [SerializeField] bool logInRelease;

        public override HotkeyId Id => id;

        public override void Execute(in HotkeyContext context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Hotkey] {context.Id} ({context.MatchedChord})");
#else
            if (logInRelease)
            {
                Debug.Log($"[Hotkey] {context.Id}");
            }
#endif
        }
    }
}
