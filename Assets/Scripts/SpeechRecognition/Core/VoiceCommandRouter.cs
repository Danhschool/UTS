using UnityEngine;
using UnityEngine.Events;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Nối STT → resolver → sự kiện Unity cho gameplay (SRP: không chứa logic Vosk).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandRouter : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionBackendBehaviour _backend;
        [SerializeField] private VoiceCommandProfile _profile;

        [Tooltip("Bật: Awake nạp JSON từ Resources (mặc định VoiceCommands/rts_voice_commands_standard_vi) vào Profile trước khi fuzzy + trước khi Vosk dùng grammar (OnEnable). Gán cùng Profile cho Vosk backend.")]
        [SerializeField] private bool _loadDatasetFromResourcesOnAwake;

        [SerializeField] private string _resourcesDatasetPath = "VoiceCommands/rts_voice_commands_standard_vi";

        [Tooltip("Nếu bật, partial cũng resolve (chỉ nên dùng để xem trước UI, dễ trùng lệnh).")]
        [SerializeField] private bool _resolvePartialsForPreview;

        [SerializeField] private UnityEvent<string> _onCommandMatched;

        [SerializeField] private UnityEvent<string, float> _onCommandMatchedWithScore;

        [SerializeField] private UnityEvent<string> _onNoCommandMatch;

        private IVoiceCommandResolver _resolver;

        private void Awake()
        {
            if (_profile == null)
            {
                Debug.LogError($"{nameof(VoiceCommandRouter)} cần {nameof(VoiceCommandProfile)}.", this);
                enabled = false;
                return;
            }

            if (_loadDatasetFromResourcesOnAwake)
            {
                var dataset = VoiceCommandDatasetFile.LoadFromResources(_resourcesDatasetPath);
                _profile.ImportFromDatasetFile(dataset);
            }

            _resolver = new FuzzyVoiceCommandResolver(_profile);
        }

        private void OnEnable()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.FinalTextCommitted += OnFinalText;
            if (_resolvePartialsForPreview)
            {
                _backend.PartialTextUpdated += OnPartialText;
            }
        }

        private void OnDisable()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.FinalTextCommitted -= OnFinalText;
            _backend.PartialTextUpdated -= OnPartialText;
        }

        /// <summary>
        /// Gọi khi đổi profile lúc chạy (hiếm); rebuild danh sách cụm fuzzy.
        /// </summary>
        public void RefreshResolver()
        {
            if (_resolver is FuzzyVoiceCommandResolver fuzzy)
            {
                fuzzy.RebuildCandidates();
            }
        }

        private void OnFinalText(string text)
        {
            TryEmit(text, isFinal: true);
        }

        private void OnPartialText(string text)
        {
            TryEmit(text, isFinal: false);
        }

        private void TryEmit(string text, bool isFinal)
        {
            if (_resolver == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!_resolver.TryResolve(text, out var commandId, out var sim))
            {
                if (isFinal)
                {
                    _onNoCommandMatch?.Invoke(text);
                }

                return;
            }

            if (isFinal || _resolvePartialsForPreview)
            {
                _onCommandMatched?.Invoke(commandId);
                _onCommandMatchedWithScore?.Invoke(commandId, sim);
            }
        }
    }
}
