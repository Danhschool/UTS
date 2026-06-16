using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>Debug session 013c46 — NDJSON append to project debug-013c46.log.</summary>
    internal static class DebugSessionLog013c46
    {
        const string LogPath = "debug-013c46.log";

        public static void Write(string hypothesisId, string location, string message, string dataJson = "{}")
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("{\"sessionId\":\"013c46\",\"hypothesisId\":\"");
                sb.Append(Escape(hypothesisId));
                sb.Append("\",\"location\":\"");
                sb.Append(Escape(location));
                sb.Append("\",\"message\":\"");
                sb.Append(Escape(message));
                sb.Append("\",\"data\":");
                sb.Append(string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson);
                sb.Append(",\"timestamp\":");
                sb.Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                sb.Append("}\n");
                File.AppendAllText(Path.Combine(Application.dataPath, "..", LogPath), sb.ToString());
            }
            catch
            {
                // ignore logging failures
            }
        }

        static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
