using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Chạy kiểm tra PvAI sau khi bootstrap (Play Mode) và ghi báo cáo + debug log.
    /// Gắn trên root Game 1 hoặc object bootstrap; chỉ chạy một lần mỗi lần vào Play.
    /// </summary>
    public sealed class PvAiRuntimeHealthCheck : MonoBehaviour
    {
        [SerializeField] bool runOnStart = true;
        [SerializeField] int framesToWait = 2;
        [SerializeField] bool logToConsole = true;

        static bool s_hasRunThisSession;

        void Start()
        {
            if (!runOnStart || s_hasRunThisSession)
            {
                return;
            }

            StartCoroutine(RunAfterBootstrap());
        }

        IEnumerator RunAfterBootstrap()
        {
            for (int i = 0; i < framesToWait; i++)
            {
                yield return null;
            }

            s_hasRunThisSession = true;
            RunValidation();
        }

        [ContextMenu("Run PvAI Health Check Now")]
        public void RunValidation()
        {
            IReadOnlyList<PvAiHealthFinding> findings = PvAiSceneValidator.Validate(playMode: true);
            EmitDebugLogs(findings);

            if (logToConsole)
            {
                string report = PvAiSceneValidator.FormatReport(findings);
                if (PvAiSceneValidator.AllPassed(findings))
                {
                    Debug.Log(report, this);
                }
                else
                {
                    Debug.LogWarning(report, this);
                }
            }
        }

        static void EmitDebugLogs(IReadOnlyList<PvAiHealthFinding> findings)
        {
            // #region agent log
            PvAiDebugSessionLog.Write(
                "H0",
                "PvAiRuntimeHealthCheck.RunValidation",
                "PvAI validation started",
                $"{{\"runId\":\"verify\",\"activeScene\":\"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\"}}");

            for (int i = 0; i < findings.Count; i++)
            {
                PvAiHealthFinding f = findings[i];
                string data = $"{{\"checkId\":\"{f.CheckId}\",\"passed\":{(f.Passed ? "true" : "false")},\"severity\":\"{f.Severity}\"}}";
                string hypo = string.IsNullOrEmpty(f.HypothesisId) ? "pass" : f.HypothesisId;
                PvAiDebugSessionLog.Write(hypo, "PvAiSceneValidator", f.Message, data);
            }

            bool allPass = PvAiSceneValidator.AllPassed(findings);
            PvAiDebugSessionLog.Write(
                "summary",
                "PvAiRuntimeHealthCheck",
                allPass ? "PvAI validation PASSED" : "PvAI validation FAILED",
                $"{{\"allPassed\":{(allPass ? "true" : "false")},\"count\":{findings.Count}}}");
            // #endregion
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionFlag() => s_hasRunThisSession = false;
    }
}
