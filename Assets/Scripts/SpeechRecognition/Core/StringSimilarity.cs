using System;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Khoảng cách Levenshtein và độ tương đồng chuẩn hóa (SRP: chỉ so khớp chuỗi).
    /// </summary>
    public static class StringSimilarity
    {
        public static int Levenshtein(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var n = a.Length;
            var m = b.Length;
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (var j = 0; j <= m; j++)
            {
                prev[j] = j;
            }

            for (var i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }

                (prev, curr) = (curr, prev);
            }

            return prev[m];
        }

        /// <summary>
        /// 1 khi trùng hoàn toàn, gần 0 khi khác nhiều (theo độ dài dài hơn).
        /// </summary>
        public static float NormalizedSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            {
                return 1f;
            }

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return 0f;
            }

            var d = Levenshtein(a.AsSpan(), b.AsSpan());
            var maxLen = Math.Max(a.Length, b.Length);
            return 1f - (float)d / maxLen;
        }
    }
}
