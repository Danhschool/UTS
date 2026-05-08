using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Ghi partial/final ra Console để kiểm tra nhanh (SRP: chỉ log).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpeechRecognitionDebugLogger : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionBackendBehaviour _backend;

        private void OnEnable()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.PartialTextUpdated += OnPartial;
            _backend.FinalTextCommitted += OnFinal;
        }

        private void OnDisable()
        {
            if (_backend == null)
            {
                return;
            }

            _backend.PartialTextUpdated -= OnPartial;
            _backend.FinalTextCommitted -= OnFinal;
        }

        private static void OnPartial(string text)
        {
            Debug.Log($"[Speech partial] {text}");
        }

        private static void OnFinal(string text)
        {
            Debug.Log($"[Speech final] {text}");
        }
    }
}
