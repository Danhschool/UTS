using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.GameEventLog;
using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// Nối CommandId (sau fuzzy voice) → thực thi gameplay qua PlayerInput.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class VoiceCommandGameplayExecutor : MonoBehaviour
    {
        [SerializeField] private VoiceCommandOneShotTranscriptMapper _mapper;
        [SerializeField] private PlayerInput _playerInput;
        [Tooltip("Prefab nhà/unit — gán để voice chọn CC/Forge/Barracks và unit chính xác.")]
        [SerializeField] private VoiceCommandGameplayPrefabs _gameplayPrefabs;

        private void Awake()
        {
            if (_mapper == null)
            {
                _mapper = GetComponent<VoiceCommandOneShotTranscriptMapper>();
            }

            if (_playerInput == null)
            {
                _playerInput = FindFirstObjectByType<PlayerInput>();
            }

            if (_playerInput != null && _gameplayPrefabs != null)
            {
                _playerInput.ConfigureVoiceCommandGameplayPrefabs(_gameplayPrefabs);
            }
        }

        private void OnEnable()
        {
            if (_mapper != null)
            {
                _mapper.CommandMatched += OnCommandMatched;
            }
        }

        private void OnDisable()
        {
            if (_mapper != null)
            {
                _mapper.CommandMatched -= OnCommandMatched;
            }
        }

        /// <summary>
        /// Mục tiêu: Chạy hành động game khi voice khớp CommandId.
        /// Cách hoạt động: Ủy quyền PlayerInput.TryExecuteVoiceCommand; log nếu thiếu PlayerInput.
        /// </summary>
        private void OnCommandMatched(string commandId)
        {
            if (_playerInput == null)
            {
                GameEventLog.Post(
                    "[VoiceCmd] Thiếu PlayerInput trong scene — không thực thi lệnh.",
                    GameEventLogCategory.Warning);
                return;
            }

            _playerInput.TryExecuteVoiceCommand(commandId);
        }
    }
}
