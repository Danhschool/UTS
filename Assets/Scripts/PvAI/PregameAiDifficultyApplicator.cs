using GameDevTV.RTS.AI;
using GameDevTV.RTS.Game.Pregame;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Áp độ khó AI đã chọn ở SSScene khi vào Game 1 offline.
    /// Gắn cùng scene với PvAiGameSceneBootstrap; kéo AIGameSessionConfigSO vào Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class PregameAiDifficultyApplicator : MonoBehaviour
    {
        [SerializeField] AIGameSessionConfigSO sessionConfig;

        void Start()
        {
            if (NetworkClient.active || NetworkServer.active || sessionConfig == null)
            {
                return;
            }

            AIController[] controllers = FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                controllers[i].SetDifficulty(sessionConfig, PregameSessionState.SelectedDifficulty);
            }
        }
    }
}
