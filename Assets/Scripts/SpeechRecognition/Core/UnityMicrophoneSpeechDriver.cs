using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Lấy âm thanh micro, resample về 16 kHz PCM16 và đẩy sang backend (SRP: capture + định tuyến).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnityMicrophoneSpeechDriver : MonoBehaviour
    {
        [SerializeField] private SpeechRecognitionBackendBehaviour _backend;
        [SerializeField] private int _deviceIndex;
        [SerializeField] private int _clipLengthSeconds = 5;
        [SerializeField] private bool _loopRecording = true;

        private AudioClip _clip;
        private string _deviceName;
        private int _lastMicPosition;
        private StreamingLinearResampler _resampler;
        private readonly List<short> _pcmScratch = new List<short>(8192);
        private float[] _mono;
        private float[] _left;
        private float[] _right;
        private float[] _segmentFloats;

        private void Awake()
        {
            if (_backend == null)
            {
                Debug.LogError($"{nameof(UnityMicrophoneSpeechDriver)} requires a {nameof(SpeechRecognitionBackendBehaviour)} reference.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_backend == null)
            {
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("No microphone devices found.", this);
                enabled = false;
                return;
            }

            _deviceIndex = Mathf.Clamp(_deviceIndex, 0, Microphone.devices.Length - 1);
            _deviceName = Microphone.devices[_deviceIndex];
            _backend.Initialize();

            var minHz = 0;
            var maxHz = 0;
            Microphone.GetDeviceCaps(_deviceName, out minHz, out maxHz);
            var sampleRate = 48000;
            if (maxHz > 0)
            {
                sampleRate = Mathf.Clamp(sampleRate, minHz, maxHz);
            }

            _clip = Microphone.Start(_deviceName, _loopRecording, _clipLengthSeconds, sampleRate);
            if (_clip == null)
            {
                Debug.LogError("Microphone.Start returned null.", this);
                enabled = false;
                return;
            }

            _resampler = new StreamingLinearResampler(_clip.frequency, 16000);
            _lastMicPosition = Microphone.GetPosition(_deviceName);
            var clipSamples = _clip.samples;
            var channels = Mathf.Max(1, _clip.channels);
            _mono = new float[clipSamples];
            _segmentFloats = new float[clipSamples];
            if (channels >= 2)
            {
                _left = new float[clipSamples];
                _right = new float[clipSamples];
            }
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(_deviceName))
            {
                Microphone.End(_deviceName);
            }

            _clip = null;
            _deviceName = null;
            _backend?.Flush();
        }

        private void Update()
        {
            if (_clip == null || _backend == null || _mono == null)
            {
                return;
            }

            var micPos = Microphone.GetPosition(_deviceName);
            var clipSamples = _clip.samples;
            var channels = _clip.channels;
            if (channels <= 0 || micPos < 0)
            {
                return;
            }

            var delta = micPos - _lastMicPosition;
            if (delta < 0)
            {
                delta += clipSamples;
            }

            if (delta == 0)
            {
                return;
            }

            _lastMicPosition = micPos;
            FillMonoBuffer(channels, clipSamples);

            var start = (micPos - delta + clipSamples) % clipSamples;
            if (_segmentFloats.Length < delta)
            {
                _segmentFloats = new float[delta];
            }

            if (start + delta <= clipSamples)
            {
                System.Array.Copy(_mono, start, _segmentFloats, 0, delta);
            }
            else
            {
                var first = clipSamples - start;
                var second = delta - first;
                System.Array.Copy(_mono, start, _segmentFloats, 0, first);
                System.Array.Copy(_mono, 0, _segmentFloats, first, second);
            }

            _pcmScratch.Clear();
            _resampler.AppendMonoFloatChunk(new System.ReadOnlySpan<float>(_segmentFloats, 0, delta), _pcmScratch);
            if (_pcmScratch.Count == 0)
            {
                return;
            }

            var arr = new short[_pcmScratch.Count];
            for (var i = 0; i < arr.Length; i++)
            {
                arr[i] = _pcmScratch[i];
            }

            _backend.AppendPcm16(arr, arr.Length);
        }

        private void FillMonoBuffer(int channels, int clipSamples)
        {
            if (channels == 1)
            {
                _clip.GetData(_mono, 0);
                return;
            }

            if (channels == 2 && _left != null && _right != null)
            {
                _clip.GetData(_left, 0);
                _clip.GetData(_right, 1);
                for (var i = 0; i < clipSamples; i++)
                {
                    _mono[i] = 0.5f * (_left[i] + _right[i]);
                }

                return;
            }

            _clip.GetData(_mono, 0);
        }
    }
}
