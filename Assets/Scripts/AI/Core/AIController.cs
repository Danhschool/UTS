using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Điều phối tick AI: registry → world state snapshot → (M2+) managers → dispatcher.
    /// Không gắn vào <see cref="GameDevTV.RTS.Player.PlayerInput"/>; không raise CommandSelectedEvent.
    /// </summary>
    public class AIController : MonoBehaviour
    {
        [SerializeField] private Owner aiOwner = Owner.AI2;
        [SerializeField] private AIDifficultySO difficultyProfile;
        [SerializeField] private float tickInterval = 1.05f;
        [SerializeField] private bool logTickSummary;
        [Header("Performance — giảm lag mỗi tick AI")]
        [Tooltip("Chia economy/base/military/influence giữa nhiều tick; tick chậm hơn khi có AIDifficultySO.")]
        [SerializeField] private bool aggressivePerformanceMode = true;
        [SerializeField] private int economyPlannerEveryNTicks = 1;
        [SerializeField] private int basePlannerEveryNTicks = 3;
        [SerializeField] private int militaryPlannerEveryNTicks = 3;
        [SerializeField] private int influenceRebuildEveryNTicks = 5;
        [SerializeField] private int configResolveEveryNTicks = 8;
        [SerializeField] private int sightRefreshEveryNTicks = 2;
        [SerializeField] private int staleGatherRecoverEveryNTicks = 4;
        [SerializeField] private int sceneCacheMinFramesBetweenRefresh = 20;
        [Tooltip("Đăng trạng thái AI lên khung sự kiện (thay spam gather +X).")]
        [SerializeField] private bool postAiStatusToGameEvent = false;
        [SerializeField] private bool postAiStatusOnlyOnChange = true;
        [Tooltip("Kéo command/supply SO vào đây; khoảng cách & placement vẫn tự tính. Để trống SO = quét map.")]
        [SerializeField] private AIEconomySettings economySettings = new();
        [Tooltip("Command SO + cap worker theo giai đoạn + reserve trước khi train (xem Base Settings).")]
        [SerializeField] private AIBaseSettings baseSettings = new();
        [Tooltip("Mở Military Settings → 3 Unit Mix Slot: gán Build Unit SO + Spawn Weight (tỷ lệ).")]
        [SerializeField] private AIMilitarySettings militarySettings = new();
        [SerializeField] private bool dispatchEconomyIntents = true;
        [SerializeField] private bool dispatchBaseIntents = true;
        [SerializeField] private bool dispatchMilitaryIntents = true;
        [Tooltip("Fog explored (minimap) — chung với người chơi; scout / gọi 100% quân đi đánh.")]
        [SerializeField] private MinimapFogSystemReference fogSystemReference;

        private AIUnitRegistry registry;
        private AIWorldState worldState;
        private AICommandDispatcher commandDispatcher;
        private AIPriorityQueue priorityQueue;
        private AIInfluenceMap influenceMap;
        private readonly List<Vector3> influenceThreatScratch = new(32);
        private readonly List<Vector3> influenceEconomicScratch = new(32);
        private AIEconomyManager economyManager;
        private AIBaseManager baseManager;
        private AIMilitaryManager militaryManager;
        private float nextTickTime;
        private int plannerTickIndex;
        private string lastPostedAiStatusLine;
        private AIEconomyRuntimeConfig cachedEconomyConfig;
        private AIBaseRuntimeConfig cachedBaseConfig;
        private AIInfluenceMapTickContext cachedInfluenceContext;
        private bool hasCachedEconomyConfig;
        private bool hasCachedBaseConfig;
        private bool hasCachedInfluenceContext;

        public Owner AiOwner => aiOwner;
        public AIDifficultySO DifficultyProfile => difficultyProfile;
        public AIUnitRegistry Registry => registry;
        public AIWorldState WorldState => worldState;
        public AICommandDispatcher CommandDispatcher => commandDispatcher;
        public AIWorldStateSnapshot CurrentSnapshot => worldState?.LastSnapshot;

        private void Awake()
        {
            registry = new AIUnitRegistry();
            worldState = new AIWorldState();
            commandDispatcher = new AICommandDispatcher(aiOwner);
            priorityQueue = new AIPriorityQueue();
            influenceMap = new AIInfluenceMap();
            economyManager = new AIEconomyManager(economySettings);
            baseManager = new AIBaseManager(baseSettings, militarySettings);
            militaryManager = new AIMilitaryManager(militarySettings, baseSettings, fogSystemReference);
        }

        private void OnEnable()
        {
            militaryManager = new AIMilitaryManager(militarySettings, baseSettings, fogSystemReference);

            if (HumanFogVisionUtility.IsHumanPlayer(aiOwner))
            {
                Debug.LogError(
                    $"[AIController] aiOwner={aiOwner} là phe human — gây điều khiển worker/người chơi sai. Đặt AI2+ trên bot (menu ProjectRTS/PvAI/M5 Fix).",
                    this);
                enabled = false;
                return;
            }

            registry.Initialize(aiOwner);
            ApplyDifficultyProfile(difficultyProfile);
            ApplyPerformanceSceneCacheThrottle();
            float interval = GetEffectiveTickInterval();
            float phase = (Mathf.Abs((int)aiOwner) % 97) / 97f * interval;
            nextTickTime = Time.time + phase;
            plannerTickIndex = 0;
        }

        private void OnDisable()
        {
            AIFactionSightQuery.ClearFaction(aiOwner);
            registry.Dispose();
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }

            nextTickTime = Time.time + GetEffectiveTickInterval();
            Tick();
        }

        /// <summary>
        /// Mục tiêu: Một nhịp quyết định AI — snapshot trạng thái (planner/dispatcher ở milestone sau).
        /// Cách hoạt động: BuildSnapshot từ registry; tùy chọn log tóm tắt debug.
        /// </summary>
        public void Tick()
        {
            plannerTickIndex++;
            AIPlannerTickPlan plan = AIPlannerTickPlan.Build(
                plannerTickIndex,
                aggressivePerformanceMode,
                economyPlannerEveryNTicks,
                basePlannerEveryNTicks,
                militaryPlannerEveryNTicks,
                influenceRebuildEveryNTicks,
                configResolveEveryNTicks,
                sightRefreshEveryNTicks,
                staleGatherRecoverEveryNTicks);

            AIWorldStateSnapshot snapshot = worldState.BuildSnapshot(registry, aiOwner);

            if (plan.RefreshSight)
            {
                AIFactionSightQuery.RefreshFromSnapshot(snapshot);
            }

            RunPlannerDispatch(snapshot, plan);
            PostAiStatusToGameEventIfNeeded(snapshot);

            if (logTickSummary)
            {
                Debug.Log(FormatTickSummaryLog(snapshot), this);
            }
        }

        /// <summary>
        /// Mục tiêu: Hiển thị trạng thái AI trên UI thay vì log từng lần gather.
        /// Cách hoạt động: Format một dòng; chỉ Post khi nội dung đổi (tránh spam).
        /// </summary>
        private void PostAiStatusToGameEventIfNeeded(AIWorldStateSnapshot snapshot)
        {
            if (!postAiStatusToGameEvent)
            {
                return;
            }

            string line = AIPlannerStatusFormatter.Format(snapshot);
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            if (postAiStatusOnlyOnChange && line == lastPostedAiStatusLine)
            {
                return;
            }

            lastPostedAiStatusLine = line;
            GameEventLog.Post(line, GameEventLogCategory.AI);
        }

        /// <summary>
        /// Mục tiêu: Mỏ hết nhưng Command vẫn Gather — reset trước khi planner gán lệnh mới.
        /// </summary>
        private static void RecoverWorkersFromStaleGather(AIWorldStateSnapshot snapshot)
        {
            if (snapshot?.Workers == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                snapshot.Workers[i]?.RecoverFromStaleGatherStateIfNeeded();
            }
        }

        /// <summary>
        /// Mục tiêu: Enqueue economy + base intents rồi dispatch theo priority chung.
        /// Cách hoạt động: Clear queue → managers enqueue → pop đến hết; unit/building qua dispatcher.
        /// </summary>
        private void RunPlannerDispatch(AIWorldStateSnapshot snapshot, AIPlannerTickPlan plan)
        {
            if (!plan.RunEconomy && !plan.RunBase && !plan.RunMilitary)
            {
                return;
            }

            if (plan.NeedsSceneEntityCache)
            {
                AISceneEntityCache.EnsureFresh();
            }

            priorityQueue.Clear();

            if (plan.RunBase || plan.RunEconomy)
            {
                AIInfraBuildOrderTracker.SyncWithWorld(snapshot);
            }

            if (plan.RecoverStaleGather)
            {
                RecoverWorkersFromStaleGather(snapshot);
            }

            AIDifficultyRuntimeOverlay difficulty = new(difficultyProfile);

            if (plan.ResolveConfigs || !hasCachedBaseConfig)
            {
                cachedBaseConfig = AIBaseConfigResolver.Resolve(snapshot, baseSettings, difficulty);
                hasCachedBaseConfig = true;
            }

            if (plan.ResolveConfigs || !hasCachedEconomyConfig)
            {
                cachedEconomyConfig = AIEconomyConfigResolver.Resolve(snapshot, economySettings);
                hasCachedEconomyConfig = true;
            }

            if (plan.RebuildInfluence || !hasCachedInfluenceContext)
            {
                cachedInfluenceContext = AIInfluenceMapTickPlanner.Build(
                    snapshot,
                    cachedBaseConfig,
                    cachedEconomyConfig.RemoteClusterMinDistance,
                    influenceMap,
                    influenceThreatScratch,
                    influenceEconomicScratch,
                    plan.UseCoarseInfluenceGrid);
                hasCachedInfluenceContext = cachedInfluenceContext.IsValid;
            }

            if (dispatchMilitaryIntents && plan.RunMilitary)
            {
                militaryManager.EnqueueIntents(
                    snapshot,
                    priorityQueue,
                    cachedInfluenceContext,
                    influenceThreatScratch,
                    difficulty);
            }

            if (dispatchBaseIntents && plan.RunBase)
            {
                baseManager.EnqueueIntents(snapshot, priorityQueue, cachedInfluenceContext, difficulty);
            }

            if (dispatchEconomyIntents && plan.RunEconomy)
            {
                economyManager.EnqueueIntents(
                    snapshot,
                    priorityQueue,
                    cachedInfluenceContext,
                    plannerTickIndex,
                    hasCachedEconomyConfig ? cachedEconomyConfig : null);
            }

            while (priorityQueue.TryPop(out AICommandIntent intent))
            {
                if (!AIWorkerCommandGuard.ShouldEnqueue(intent))
                {
                    if (intent.Command is BuildBuildingCommand buildCommand
                        && intent.Entity is Worker skippedWorker
                        && buildCommand.Building != null)
                    {
                        AIInfraBuildOrderTracker.ClearOrder(aiOwner, buildCommand.Building.Name);
                        AIConstructionAssignment.ReleaseBuilderIfMatches(skippedWorker.GetInstanceID());
                    }

                    continue;
                }

                if (intent.Command is not BaseCommand command)
                {
                    continue;
                }

                if (intent.Entity is AbstractUnit unit)
                {
                    commandDispatcher.TryDispatchSpecificCommand(
                        unit,
                        command,
                        intent.Hit,
                        intent.UnitIndex,
                        intent.MouseButton);
                }
                else if (intent.Entity is BaseBuilding building)
                {
                    commandDispatcher.DispatchBuildingCommand(building, command, intent.Hit);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Log debug dễ đọc — liệt kê tên unit/building thay vì chỉ đếm.
        /// Cách hoạt động: Gom theo <see cref="UnlockableSO.Name"/>; pop lấy từ số unit trong snapshot (không dùng Supplies cho AI).
        /// </summary>
        private string FormatTickSummaryLog(AIWorldStateSnapshot snapshot)
        {
            StringBuilder sb = new(256);
            sb.Append("[AI ").Append(snapshot.Owner).Append("] ");
            sb.Append("S/W/F=").Append(snapshot.Stone).Append('/').Append(snapshot.Wood).Append('/').Append(snapshot.Food);
            sb.Append(" pop=").Append(snapshot.Units.Count);
            if (snapshot.PopulationLimit > 0)
            {
                sb.Append('/').Append(snapshot.PopulationLimit);
            }

            sb.Append(" | gather-nodes=").Append(snapshot.GatherableSupplies.Count);
            sb.AppendLine();
            AppendNamedEntityList(sb, "Units", snapshot.Units.Count, CountUnitsByDisplayName(snapshot.Units));
            AppendNamedEntityList(sb, "Buildings", snapshot.Buildings.Count, CountBuildingsByDisplayName(snapshot.Buildings));
            return sb.ToString();
        }

        private static void AppendNamedEntityList(StringBuilder sb, string label, int total, Dictionary<string, int> countsByName)
        {
            sb.Append("  ").Append(label).Append(" (").Append(total).Append("):");
            if (countsByName.Count == 0)
            {
                sb.AppendLine(" (none)");
                return;
            }

            sb.AppendLine();
            foreach (KeyValuePair<string, int> entry in countsByName)
            {
                sb.Append("    - ").Append(entry.Key);
                if (entry.Value > 1)
                {
                    sb.Append(" x").Append(entry.Value);
                }

                sb.AppendLine();
            }
        }

        private static Dictionary<string, int> CountUnitsByDisplayName(IReadOnlyList<AbstractUnit> units)
        {
            Dictionary<string, int> counts = new(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                string displayName = GetUnitDisplayName(unit);
                counts.TryGetValue(displayName, out int count);
                counts[displayName] = count + 1;
            }

            return counts;
        }

        private static Dictionary<string, int> CountBuildingsByDisplayName(IReadOnlyList<BaseBuilding> buildings)
        {
            Dictionary<string, int> counts = new(buildings.Count);
            for (int i = 0; i < buildings.Count; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null)
                {
                    continue;
                }

                string displayName = GetBuildingDisplayName(building);
                counts.TryGetValue(displayName, out int count);
                counts[displayName] = count + 1;
            }

            return counts;
        }

        private static string GetUnitDisplayName(AbstractUnit unit)
        {
            string soName = unit.UnitSO != null ? unit.UnitSO.Name : null;
            string role = unit is Worker ? "Worker" : unit.GetType().Name;
            return string.IsNullOrEmpty(soName) ? role : $"{soName} ({role})";
        }

        private static string GetBuildingDisplayName(BaseBuilding building)
        {
            if (building.BuildingSO != null)
            {
                return building.BuildingSO.Name;
            }

            return building.UnitSO != null ? building.UnitSO.Name : building.GetType().Name;
        }

        /// <summary>
        /// Mục tiêu: Đổi độ khó runtime (menu / GameSetup / sandbox Inspector).
        /// Cách hoạt động: Gán SO và áp tick interval từ profile.
        /// </summary>
        public void SetDifficulty(AIDifficultySO profile)
        {
            difficultyProfile = profile;
            ApplyDifficultyProfile(profile);
        }

        /// <summary>
        /// Mục tiêu: Áp độ khó từ <see cref="AIGameSessionConfigSO"/> theo enum lobby.
        /// Cách hoạt động: Resolve asset rồi gọi <see cref="SetDifficulty"/>.
        /// </summary>
        public void SetDifficulty(AIGameSessionConfigSO session, AIDifficultyLevel level)
        {
            if (session == null)
            {
                return;
            }

            SetDifficulty(session.Resolve(level));
        }

        private void ApplyDifficultyProfile(AIDifficultySO profile)
        {
            if (profile == null)
            {
                return;
            }

            tickInterval = profile.TickInterval;
            ApplyPerformanceSceneCacheThrottle();
        }

        /// <summary>
        /// Mục tiêu: Tick AI chậm hơn khi bật performance mode (giảm spike định kỳ).
        /// Cách hoạt động: Nhân interval SO/Inspector với hệ số khi aggressivePerformanceMode.
        /// </summary>
        private float GetEffectiveTickInterval()
        {
            float baseInterval = difficultyProfile != null ? difficultyProfile.TickInterval : tickInterval;
            if (!aggressivePerformanceMode)
            {
                return baseInterval;
            }

            return Mathf.Max(0.85f, baseInterval * 1.35f);
        }

        private void ApplyPerformanceSceneCacheThrottle()
        {
            int frames = aggressivePerformanceMode
                ? Mathf.Max(sceneCacheMinFramesBetweenRefresh, 12)
                : 1;
            AISceneEntityCache.SetMinFramesBetweenRefresh(frames);
        }

        /// <summary>
        /// Mục tiêu: Đổi phe AI hoặc độ khó runtime (GameSetup / menu).
        /// Cách hoạt động: Cập nhật owner, registry và dispatcher.
        /// </summary>
        public void SetAiOwner(Owner owner)
        {
            if (aiOwner == owner)
            {
                return;
            }

            aiOwner = owner;
            commandDispatcher = new AICommandDispatcher(aiOwner);
            economyManager = new AIEconomyManager(economySettings);
            baseManager = new AIBaseManager(baseSettings, militarySettings);
            militaryManager = new AIMilitaryManager(militarySettings, baseSettings, fogSystemReference);

            if (isActiveAndEnabled)
            {
                registry.Initialize(aiOwner);
            }
        }
    }
}
