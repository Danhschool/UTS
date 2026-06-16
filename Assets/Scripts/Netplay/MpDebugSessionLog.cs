using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>Ghi NDJSON debug session MP — chỉ dùng khi debug.</summary>
    public static class MpDebugSessionLog
    {
        const string SessionId = "013c46";
        const string LogFileName = "debug-013c46.log";

        public static void Write(string hypothesisId, string location, string message, string dataJson = "{}")
        {
            // #region agent log
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return;
                }

                string path = Path.Combine(projectRoot, LogFileName);
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string line =
                    $"{{\"sessionId\":\"{SessionId}\",\"hypothesisId\":\"{Escape(hypothesisId)}\",\"location\":\"{Escape(location)}\",\"message\":\"{Escape(message)}\",\"data\":{dataJson},\"timestamp\":{ts}}}\n";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
            }
            // #endregion
        }

        static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
