using System;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Unity-facing hook for backends. Lets the microphone driver reference a single inspector field (OCP: swap backends).
    /// </summary>
    public abstract class SpeechRecognitionBackendBehaviour : MonoBehaviour, ISpeechRecognitionBackend
    {
        public event Action<string> PartialTextUpdated;
        public event Action<string> FinalTextCommitted;

        public abstract void Initialize();
        public abstract void AppendPcm16(short[] buffer, int sampleCount);
        public abstract void Flush();

        protected void RaisePartial(string text)
        {
            PartialTextUpdated?.Invoke(text);
        }

        protected void RaiseFinal(string text)
        {
            FinalTextCommitted?.Invoke(text);
        }
    }
}
