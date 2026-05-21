using System.Collections.Generic;
using System.Text;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
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
        [SerializeField] private float tickInterval = 0.65f;
        [SerializeField] private bool logTickSummary;
        [Tooltip("Kéo command/supply SO vào đây; khoảng cách & placement vẫn tự tính. Để trống SO = quét map.")]
        [SerializeField] private AIEconomySettings economySettings = new();
        [Tooltip("Command SO + cap worker theo giai đoạn + reserve trước khi train (xem Base Settings).")]
        [SerializeField] private AIBaseSettings baseSettings = new();
        [SerializeField] private bool dispatchEconomyIntents = true;
        [SerializeField] private bool dispatchBaseIntents = true;

        private AIUnitRegistry registry;
        private AIWorldState worldState;
        private AICommandDispatcher commandDispatcher;
        private AIPriorityQueue priorityQueue;
        private AIEconomyManager economyManager;
        private AIBaseManager baseManager;
        private float nextTickTime;

        public Owner AiOwner => aiOwner;
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
            economyManager = new AIEconomyManager(economySettings);
            baseManager = new AIBaseManager(baseSettings);
        }

        private void OnEnable()
        {
            registry.Initialize(aiOwner);
            nextTickTime = Time.time;
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

            nextTickTime = Time.time + tickInterval;
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

            if (logTickSummary)
            {
                Debug.Log(FormatTickSummaryLog(snapshot), this);
            }
        }

        /// <summary>
        /// Mục tiêu: Enqueue economy + base intents rồi dispatch theo priority chung.
        /// Cách hoạt động: Clear queue → managers enqueue → pop đến hết; unit/building qua dispatcher.
        /// </summary>
        private void RunPlannerDispatch(AIWorldStateSnapshot snapshot)
        {
            priorityQueue.Clear();
            AIInfraBuildOrderTracker.SyncWithWorld(snapshot);

            if (dispatchBaseIntents)
            {
                baseManager.EnqueueIntents(snapshot, priorityQueue);
            }

            if (dispatchEconomyIntents)
            {
                economyManager.EnqueueIntents(snapshot, priorityQueue);
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
            baseManager = new AIBaseManager(baseSettings);

            if (isActiveAndEnabled)
            {
                registry.Initialize(aiOwner);
            }
        }
    }
}
