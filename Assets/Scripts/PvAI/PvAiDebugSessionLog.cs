using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// Ghi NDJSON vào debug-559c4e.log (workspace root) khi kiểm tra PvAI ở Play Mode.
    /// </summary>
    public static class PvAiDebugSessionLog
    {
        const string SessionId = "559c4e";
        const string LogFileName = "debug-559c4e.log";

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
                string line = $"{{\"sessionId\":\"{SessionId}\",\"hypothesisId\":\"{Escape(hypothesisId)}\",\"location\":\"{Escape(location)}\",\"message\":\"{Escape(message)}\",\"data\":{dataJson},\"timestamp\":{ts}}}\n";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
                // Không làm gián đoạn gameplay khi ghi log thất bại.
            }
            // #endregion
        }

        static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
