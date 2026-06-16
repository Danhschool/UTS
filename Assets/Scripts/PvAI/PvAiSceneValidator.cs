using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Kiểm tra invariant M5 PvAI (Game 1 offline) — dùng chung Editor và Play Mode.
    /// </summary>
    public static class PvAiSceneValidator
    {
        public const string Game1SceneName = "Game 1";

        /// <summary>
        /// Mục tiêu: Trả về danh sách pass/fail cho scene PvAI hiện tại.
        /// Cách hoạt động: Quét component/owner; nếu playMode thì thêm kiểm tra LocalHumanOwner và Mirror.
        /// </summary>
        public static IReadOnlyList<PvAiHealthFinding> Validate(bool playMode)
        {
            var findings = new List<PvAiHealthFinding>(16);
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.name.Contains("Game 1") && !scene.path.Contains("Game 1"))
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "scene_name",
                    "H0",
                    PvAiHealthSeverity.Warning,
                    $"Scene đang mở là '{scene.name}' — checklist M5 áp dụng cho '{Game1SceneName}'."));
            }

            ValidateRequiredBootstrap(findings);
            ValidatePvAiSpawnSetup(findings);
            ValidateAiControllers(findings);
            ValidateBotPlayer2Migration(findings);
            ValidateFactionOwnership(findings);

            if (playMode)
            {
                ValidateOfflineLocalOwner(findings);
                ValidateMirrorInactive(findings);
            }

            return findings;
        }

        public static bool AllPassed(IReadOnlyList<PvAiHealthFinding> findings)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity != PvAiHealthSeverity.Info)
                {
                    return false;
                }
            }

            return true;
        }

        public static string FormatReport(IReadOnlyList<PvAiHealthFinding> findings)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("[PvAI Health] Báo cáo kiểm tra M5 (Game 1 offline):");
            int errors = 0;
            int warnings = 0;

            for (int i = 0; i < findings.Count; i++)
            {
                PvAiHealthFinding f = findings[i];
                if (f.Severity == PvAiHealthSeverity.Info)
                {
                    sb.AppendLine($"  OK  [{f.CheckId}] {f.Message}");
                }
                else
                {
                    string tag = f.Severity == PvAiHealthSeverity.Error ? "ERR" : "WRN";
                    sb.AppendLine($"  {tag} [{f.CheckId}] {f.Message}");
                    if (f.Severity == PvAiHealthSeverity.Error)
                    {
                        errors++;
                    }
                    else
                    {
                        warnings++;
                    }
                }
            }

            sb.AppendLine($"Tổng: {findings.Count} mục — lỗi={errors}, cảnh báo={warnings}, pass={findings.Count - errors - warnings}.");
            return sb.ToString();
        }

        static void ValidateRequiredBootstrap(List<PvAiHealthFinding> findings)
        {
            LocalHumanOwnerBootstrap bootstrap = Object.FindFirstObjectByType<LocalHumanOwnerBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "bootstrap_local_human",
                    "H4",
                    PvAiHealthSeverity.Error,
                    "Thiếu LocalHumanOwnerBootstrap — offline không gán LocalOwner=Player1."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "bootstrap_local_human",
                    "Có LocalHumanOwnerBootstrap."));
            }

            AIController[] controllers = Object.FindObjectsByType<AIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "ai_controller_present",
                    "H4",
                    PvAiHealthSeverity.Error,
                    "Không có AIController trong scene — PvAI không chạy bot."));
            }
            else
            {
                int disabled = 0;
                for (int i = 0; i < controllers.Length; i++)
                {
                    if (controllers[i] != null && !controllers[i].enabled)
                    {
                        disabled++;
                    }
                }

                if (disabled > 0)
                {
                    findings.Add(PvAiHealthFinding.Fail(
                        "ai_controller_enabled",
                        "H4",
                        PvAiHealthSeverity.Warning,
                        $"{disabled}/{controllers.Length} AIController bị disabled."));
                }
                else
                {
                    findings.Add(PvAiHealthFinding.Pass(
                        "ai_controller_enabled",
                        $"Có {controllers.Length} AIController và đều enabled."));
                }
            }

            PlayerViewBinder binder = Object.FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            findings.Add(binder == null
                ? PvAiHealthFinding.Fail(
                    "player_view_binder",
                    "H4",
                    PvAiHealthSeverity.Warning,
                    "Thiếu PlayerViewBinder — fog/HUD có thể không bind theo LocalOwner.")
                : PvAiHealthFinding.Pass("player_view_binder", "Có PlayerViewBinder."));
        }

        static void ValidatePvAiSpawnSetup(List<PvAiHealthFinding> findings)
        {
            PvAiGameSceneSetup setup = Object.FindFirstObjectByType<PvAiGameSceneSetup>(FindObjectsInactive.Include);
            if (setup == null)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "pvai_spawn_setup",
                    "H8",
                    PvAiHealthSeverity.Error,
                    "Thiếu PvAiGameSceneSetup — chạy menu ProjectRTS/PvAI/Setup Game 1 spawn (long-term)."));
                return;
            }

            findings.Add(PvAiHealthFinding.Pass("pvai_spawn_setup", "Có PvAiGameSceneSetup."));

            if (setup.civilCentralPrefab == null)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "pvai_cc_prefab",
                    "H8",
                    PvAiHealthSeverity.Error,
                    "PvAiGameSceneSetup thiếu civilCentralPrefab."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass("pvai_cc_prefab", "Đã gán civilCentralPrefab."));
            }

            ValidateStartingUnitPrefabs(setup, findings);

            bool spawnOk = setup.factionSpawnPoints != null
                           && setup.factionSpawnPoints.Length >= 2
                           && setup.factionSpawnPoints[0] != null
                           && setup.factionSpawnPoints[1] != null;
            findings.Add(spawnOk
                ? PvAiHealthFinding.Pass("pvai_spawn_points", "Đủ 2 faction spawn points.")
                : PvAiHealthFinding.Fail(
                    "pvai_spawn_points",
                    "H8",
                    PvAiHealthSeverity.Error,
                    "Thiếu factionSpawnPoints[0] (human) hoặc [1] (AI)."));

            PvAiGameSceneBootstrap bootstrap = Object.FindFirstObjectByType<PvAiGameSceneBootstrap>(FindObjectsInactive.Include);
            findings.Add(bootstrap == null
                ? PvAiHealthFinding.Fail(
                    "pvai_spawn_bootstrap",
                    "H8",
                    PvAiHealthSeverity.Warning,
                    "Thiếu PvAiGameSceneBootstrap — spawn PvE có thể không chạy.")
                : PvAiHealthFinding.Pass("pvai_spawn_bootstrap", "Có PvAiGameSceneBootstrap."));

            int sceneCc = CountScenePlacedCivilCentrals();
            if (sceneCc > 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "pvai_no_scene_cc",
                    "H8",
                    PvAiHealthSeverity.Warning,
                    $"{sceneCc} civil_central cắm sẵn trong scene — nên xóa (menu Setup) để tránh trùng MP/PvE."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "pvai_no_scene_cc",
                    "Không có civil_central cắm sẵn trong scene (đúng long-term)."));
            }
        }

        static void ValidateStartingUnitPrefabs(PvAiGameSceneSetup setup, List<PvAiHealthFinding> findings)
        {
            if (!setup.spawnStartingUnits)
            {
                return;
            }

            if (setup.startingUnits == null || setup.startingUnits.Length == 0)
            {
                if (setup.startingWorkerPrefab != null
                    && !PvAiStartingUnitPrefabValidator.TryValidate(
                        setup.startingWorkerPrefab,
                        out string legacyError))
                {
                    findings.Add(PvAiHealthFinding.Fail(
                        "pvai_starting_worker_unitso",
                        "H8",
                        PvAiHealthSeverity.Error,
                        legacyError));
                }

                return;
            }

            for (int i = 0; i < setup.startingUnits.Length; i++)
            {
                StartingUnitSpawnEntry entry = setup.startingUnits[i];
                if (entry.unitPrefab == null || entry.count <= 0)
                {
                    continue;
                }

                if (PvAiStartingUnitPrefabValidator.TryValidate(entry.unitPrefab, out _))
                {
                    continue;
                }

                findings.Add(PvAiHealthFinding.Fail(
                    "pvai_starting_units_unitso",
                    "H8",
                    PvAiHealthSeverity.Error,
                    $"startingUnits[{i}] ({entry.unitPrefab.name}): thiếu UnitSO — dùng Worker 1.prefab hoặc chạy Prepare Game 1 Scene."));
            }
        }

        static int CountScenePlacedCivilCentrals()
        {
            int count = 0;
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building != null
                    && building.gameObject.name.ToLowerInvariant().Contains("civil_central"))
                {
                    count++;
                }
            }

            return count;
        }

        static void ValidateAiControllers(List<PvAiHealthFinding> findings)
        {
            AIController[] controllers = Object.FindObjectsByType<AIController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Scene activeScene = SceneManager.GetActiveScene();
            int wrongOwner = 0;
            var wrongDetails = new List<string>(4);

            for (int i = 0; i < controllers.Length; i++)
            {
                AIController c = controllers[i];
                if (c == null)
                {
                    continue;
                }

                if (!BelongsToActiveGameplayScene(c.gameObject, activeScene))
                {
                    continue;
                }

                Owner o = c.AiOwner;
                if (HumanFogVisionUtility.IsHumanPlayer(o))
                {
                    wrongOwner++;
                    wrongDetails.Add($"{c.gameObject.name} owner={(int)o} scene={c.gameObject.scene.name}");
                }
            }

            if (wrongOwner > 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "ai_controller_owner",
                    "H2",
                    PvAiHealthSeverity.Error,
                    $"{wrongOwner} AIController human owner trong scene active — {string.Join("; ", wrongDetails)} (menu M5 Fix)."));
            }
            else if (controllers.Length > 0)
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "ai_controller_owner",
                    "Mọi AIController trong scene active dùng phe AI (không phải human Player1/2)."));
            }
        }

        static bool BelongsToActiveGameplayScene(GameObject go, Scene activeScene)
        {
            if (go == null || !activeScene.IsValid())
            {
                return false;
            }

            return go.scene == activeScene;
        }

        static void ValidateBotPlayer2Migration(List<PvAiHealthFinding> findings)
        {
            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int staleBotPlayer2 = 0;
            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable cmd = commandables[i];
                if (cmd == null || cmd.Owner != Owner.Player2)
                {
                    continue;
                }

                if (LooksLikeBotName(cmd.gameObject.name))
                {
                    staleBotPlayer2++;
                }
            }

            if (staleBotPlayer2 > 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "bot_owner_player2",
                    "H3",
                    PvAiHealthSeverity.Error,
                    $"{staleBotPlayer2} unit bot vẫn Owner=Player2 — migrate sang AI2 (menu M5 Fix)."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "bot_owner_player2",
                    "Không có bot heuristic nào còn Owner=Player2."));
            }
        }

        static void ValidateFactionOwnership(List<PvAiHealthFinding> findings)
        {
            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int humanP1 = 0;
            int aiFaction = 0;
            int aiOnPlayer1 = 0;

            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable cmd = commandables[i];
                if (cmd == null)
                {
                    continue;
                }

                if (cmd.Owner == Owner.Player1)
                {
                    humanP1++;
                    if (LooksLikeBotName(cmd.gameObject.name))
                    {
                        aiOnPlayer1++;
                    }
                }
                else if (cmd.Owner == Owner.AI2 || cmd.Owner == Owner.AI3)
                {
                    aiFaction++;
                }
            }

            if (humanP1 == 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "human_units_player1",
                    "H6",
                    PvAiHealthSeverity.Warning,
                    "Không có unit nào Owner=Player1 trong scene (kiểm tra spawn Civil Central)."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "human_units_player1",
                    $"Có {humanP1} commandable Owner=Player1."));
            }

            if (aiFaction == 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "ai_units_present",
                    "H6",
                    PvAiHealthSeverity.Warning,
                    "Không có unit AI2/AI3 — bot có thể chưa spawn hoặc owner sai."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "ai_units_present",
                    $"Có {aiFaction} commandable phe AI (AI2/AI3)."));
            }

            if (aiOnPlayer1 > 0)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "bot_on_player1",
                    "H7",
                    PvAiHealthSeverity.Error,
                    $"{aiOnPlayer1} object tên giống bot nhưng Owner=Player1."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass("bot_on_player1", "Không có bot heuristic trên Player1."));
            }
        }

        static void ValidateOfflineLocalOwner(List<PvAiHealthFinding> findings)
        {
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "local_owner_service",
                    "H1",
                    PvAiHealthSeverity.Error,
                    "Play Mode: LocalHumanOwnerService.Instance null sau bootstrap."));
                return;
            }

            if (!service.IsInitialized)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "local_owner_initialized",
                    "H1",
                    PvAiHealthSeverity.Error,
                    "LocalHumanOwnerService chưa initialized — LocalOwner không hợp lệ."));
            }
            else if (service.LocalOwner != Owner.Player1)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "local_owner_player1",
                    "H1",
                    PvAiHealthSeverity.Error,
                    $"Offline PvAI cần LocalOwner=Player1, hiện tại={service.LocalOwner}."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "local_owner_player1",
                    "LocalOwner=Player1 (offline M5)."));
            }
        }

        static void ValidateMirrorInactive(List<PvAiHealthFinding> findings)
        {
            bool mirror = NetworkClient.active || NetworkServer.active;
            if (mirror)
            {
                findings.Add(PvAiHealthFinding.Fail(
                    "mirror_inactive",
                    "H5",
                    PvAiHealthSeverity.Warning,
                    "Mirror đang active — đây không phải PvAI offline thuần (kiểm tra NetworkManager)."));
            }
            else
            {
                findings.Add(PvAiHealthFinding.Pass(
                    "mirror_inactive",
                    "Mirror không active — đúng chế độ offline Game 1."));
            }
        }

        internal static bool LooksLikeBotName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            string lower = objectName.ToLowerInvariant();
            return lower.Contains("ai")
                   || lower.Contains("bot")
                   || lower.Contains("enemy")
                   || lower.Contains("opponent");
        }
    }
}
