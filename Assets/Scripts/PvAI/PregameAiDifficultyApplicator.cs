using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Hook scene Game 1 — gọi <see cref="PregameAiDifficultyApplyService"/> khi vào play mode.
    /// Có thể gắn cùng PvAiGameSceneBootstrap; không bắt buộc nếu bootstrap đã gọi service.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PregameAiDifficultyApplicator : MonoBehaviour
    {
        [SerializeField] bool logWhenApplied = true;

        void Start()
        {
            PregameAiDifficultyApplyService.TryApply(logWhenApplied);
        }
    }
}
