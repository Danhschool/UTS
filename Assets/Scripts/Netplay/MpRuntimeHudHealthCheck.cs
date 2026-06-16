using System.Collections;
using System.Text;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Báo cáo HUD MP sau khi vào Game 1/2 — giúp phân biệt "scene đủ component" vs "runtime chạy đúng".
    /// </summary>
    public sealed class MpRuntimeHudHealthCheck : MonoBehaviour
    {
        static bool _ranThisSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ScheduleOnGameplayScene()
        {
            if (!GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            var host = new GameObject(nameof(MpRuntimeHudHealthCheck));
            host.AddComponent<MpRuntimeHudHealthCheck>();
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(2.5f);

            if (_ranThisSession || !GameplayStartupScenes.IsActiveGameplayScene())
            {
                Destroy(gameObject);
                yield break;
            }

            _ranThisSession = true;
            RunAndLog();
            Destroy(gameObject);
        }

        /// <summary>
        /// Mục tiêu: In báo cáo một lần — LocalOwner, HUD active, rig wire, Bus UI.
        /// Cách hoạt động: Quét scene sau Director/retry; LogWarning nếu có lỗi cấu hình.
        /// </summary>
        public static void RunAndLog()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("[MP HUD Health] Báo cáo sau load map:");

            bool isMp = RtsNetplaySession.IsNetworkMatch;
            bool isPureClient = NetworkClient.active && !NetworkServer.active;
            sb.AppendLine($"  Mirror MP={isMp}, pureClient={isPureClient}");

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            Owner local = service != null && service.IsInitialized
                ? service.LocalOwner
                : LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            sb.AppendLine($"  LocalOwner={local}, teamIndex={MpLocalOwnerSceneSync.ResolveLocalTeamIndex()}");

            MpPlayerPresentationDirector director =
                Object.FindFirstObjectByType<MpPlayerPresentationDirector>(FindObjectsInactive.Include);
            sb.AppendLine($"  Director={(director != null ? "OK" : "MISSING")}, hasRigs={director?.HasConfiguredRigs}");

            PlayerViewBinder legacyBinder =
                Object.FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            if (legacyBinder != null && legacyBinder.enabled && isMp && director?.HasConfiguredRigs == true)
            {
                sb.AppendLine("  WARN: PlayerViewBinder vẫn ENABLED — xung đột Director (chạy Prepare Game 1).");
            }

            LogHudBranch(sb, Owner.Player1, local);
            LogHudBranch(sb, Owner.Player2, local);

            int busUi = CountConfiguredRuntimeUi(local);
            sb.AppendLine($"  RuntimeUI configured for local={busUi}");

            Supplies localSupplies = MpHudSuppliesResolver.FindForOwner(local);
            if (localSupplies != null)
            {
                sb.AppendLine(
                    $"  Supplies local: {localSupplies.name}, active={localSupplies.gameObject.activeInHierarchy}, "
                    + $"root={localSupplies.transform.root.name}");

                LogSuppliesTextRefs(sb, localSupplies);
            }
            else
            {
                sb.AppendLine("  ERROR: Không tìm thấy Supplies HUD cho LocalOwner.");
            }

            if (isPureClient && RtsUtsSupplyStateSync.Instance == null)
            {
                sb.AppendLine("  ERROR: RtsUtsSupplyStateSync.Instance null trên client — S/W/F không sync.");
            }
            else if (isPureClient)
            {
                sb.AppendLine("  SupplyStateSync=OK");
            }

            bool hasErrors = sb.ToString().Contains("ERROR:") || sb.ToString().Contains("WARN:");
            if (hasErrors)
            {
                Debug.LogWarning(sb.ToString());
            }
            else
            {
                Debug.Log(sb.ToString());
            }
        }

        static void LogHudBranch(StringBuilder sb, Owner branch, Owner localOwner)
        {
            Supplies supplies = MpHudSuppliesResolver.FindForOwner(branch);
            if (supplies == null)
            {
                sb.AppendLine($"  {branch}: Supplies MISSING");
                return;
            }

            GameObject root = supplies.transform.root.gameObject;
            bool shouldBeActive = branch == localOwner;
            RuntimeUI[] uis = root.GetComponentsInChildren<RuntimeUI>(true);
            int configured = 0;
            for (int i = 0; i < uis.Length; i++)
            {
                if (uis[i] != null && uis[i].IsConfiguredForOwner(branch))
                {
                    configured++;
                }
            }

            MpPlayerPresentationRig rig = supplies.GetComponentInParent<MpPlayerPresentationRig>(true);
            sb.AppendLine(
                $"  {branch}: root={root.name}, active={root.activeSelf} (expect {shouldBeActive}), "
                + $"busUi={configured}, rig={(rig != null ? rig.name : "none")}");
        }

        static int CountConfiguredRuntimeUi(Owner owner)
        {
            RuntimeUI[] runtimeUis = Object.FindObjectsByType<RuntimeUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui != null && ui.IsConfiguredForOwner(owner))
                {
                    count++;
                }
            }

            return count;
        }

        static void LogSuppliesTextRefs(StringBuilder sb, Supplies supplies)
        {
            supplies.TryGetSupplyTextWireStatus(
                out bool stoneWired,
                out bool woodWired,
                out bool foodWired,
                out bool popWired);
            if (!stoneWired || !woodWired || !foodWired)
            {
                sb.AppendLine(
                    $"  WARN: Supplies text refs — stone={stoneWired}, wood={woodWired}, food={foodWired}, pop={popWired}");
            }
        }
    }
}
