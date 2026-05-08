using System;
using System.Collections.Generic;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Chuyển luồng mono float theo tần số nguồn sang PCM16 16 kHz bằng nội suy tuyến tính (SRP: chỉ resample).
    /// Giữ pha giữa các lần gọi để micro dạng vòng hoạt động ổn định.
    /// </summary>
    public sealed class StreamingLinearResampler
    {
        private readonly double _sourceStepPerOutputSample;
        private double _sourceCursor;

        public StreamingLinearResampler(int sourceSampleRate, int destinationSampleRate = 16000)
        {
            if (sourceSampleRate <= 0 || destinationSampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSampleRate));
            }

            _sourceStepPerOutputSample = (double)sourceSampleRate / destinationSampleRate;
        }

        public void ResetPhase()
        {
            _sourceCursor = 0d;
        }

        /// <summary>
        /// Xử lý một khối mẫu mono liên tục; kết quả nối vào <paramref name="destination"/>.
        /// </summary>
        public void AppendMonoFloatChunk(ReadOnlySpan<float> chunk, List<short> destination)
        {
            var n = chunk.Length;
            if (n == 0)
            {
                return;
            }

            while (true)
            {
                var i0 = (int)Math.Floor(_sourceCursor);
                if (i0 >= n)
                {
                    _sourceCursor -= n;
                    return;
                }

                var frac = (float)(_sourceCursor - i0);
                var s0 = chunk[i0];
                var s1 = i0 + 1 < n ? chunk[i0 + 1] : s0;
                var sample = s0 + (s1 - s0) * frac;
                destination.Add(FloatToPcm16(sample));
                _sourceCursor += _sourceStepPerOutputSample;
            }
        }

        private static short FloatToPcm16(float sample)
        {
            var v = sample * 32767f;
            if (v > 32767f)
            {
                v = 32767f;
            }

            if (v < -32768f)
            {
                v = -32768f;
            }

            return (short)v;
        }
    }
}
