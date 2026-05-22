using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Minimap;
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
        [SerializeField] private float tickInterval = 0.65f;
        [SerializeField] private bool logTickSummary;
        [Tooltip("Đăng trạng thái AI lên khung sự kiện (thay spam gather +X).")]
        [SerializeField] private bool postAiStatusToGameEvent = true;
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
        [Tooltip("Fog explored (minimap) — dùng biết map đã khám phá hết để gọi 100% quân đi đánh.")]
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
            registry.Initialize(aiOwner);
            ApplyDifficultyProfile(difficultyProfile);
            nextTickTime = Time.time;
            plannerTickIndex = 0;
        }

        private void OnDisable()
        {
            registry.Dispose();
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }

            float interval = difficultyProfile != null ? difficultyProfile.TickInterval : tickInterval;
            nextTickTime = Time.time + interval;
            Tick();
        }

        /// <summary>
        /// Mục tiêu: Một nhịp quyết định AI — snapshot trạng thái (planner/dispatcher ở milestone sau).
        /// Cách hoạt động: BuildSnapshot từ registry; tùy chọn log tóm tắt debug.
        /// </summary>
        public void Tick()
        {
            AIWorldStateSnapshot snapshot = worldState.BuildSnapshot(registry, aiOwner);

            RunPlannerDispatch(snapshot);
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
        private void RunPlannerDispatch(AIWorldStateSnapshot snapshot)
        {
            priorityQueue.Clear();
            plannerTickIndex++;
            AIInfraBuildOrderTracker.SyncWithWorld(snapshot);
            RecoverWorkersFromStaleGather(snapshot);

            AIDifficultyRuntimeOverlay difficulty = new(difficultyProfile);
            AIBaseRuntimeConfig baseConfig = AIBaseConfigResolver.Resolve(snapshot, baseSettings, difficulty);
            AIEconomyRuntimeConfig economyConfig = AIEconomyConfigResolver.Resolve(snapshot, economySettings);
            AIInfluenceMapTickContext influence = AIInfluenceMapTickPlanner.Build(
                snapshot,
                baseConfig,
                economyConfig.RemoteClusterMinDistance,
                influenceMap,
                influenceThreatScratch,
                influenceEconomicScratch);

            if (dispatchMilitaryIntents)
            {
                militaryManager.EnqueueIntents(snapshot, priorityQueue, influence, influenceThreatScratch, difficulty);
            }

            if (dispatchBaseIntents)
            {
                baseManager.EnqueueIntents(snapshot, priorityQueue, influence, difficulty);
            }

            if (dispatchEconomyIntents)
            {
                economyManager.EnqueueIntents(snapshot, priorityQueue, influence, plannerTickIndex);
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
