using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Worker gather, cân Stone/Wood/Food, đề xuất Store gần cụm mỏ xa Civil Central.
    /// Mỗi worker: <see cref="GatherCommand"/> → BT Gather Sub Graph tự loop + tự return khi đầy (Petra: không spam ReturnSupplies).
    /// Chỉ mỏ <see cref="GatherableSupply.IsVisible"/>; khóa gather trên <see cref="Worker.ShouldIssueGatherTo"/>.
    /// </summary>
    public sealed class AIEconomyManager
    {
        private readonly AIEconomySettings manualOverrides;
        private readonly List<AbstractUnitSO> depositTypeScratch = new(4);
        private readonly List<Worker> idleGatherWorkersScratch = new(32);
        private readonly HashSet<int> reservedGatherSupplyThisTick = new(32);

        public AIEconomyManager(AIEconomySettings manualOverrides = null)
        {
            this.manualOverrides = manualOverrides ?? AIEconomySettings.Default;
        }

        /// <summary>
        /// Mục tiêu: Sinh intent economy cho tick hiện tại.
        /// Cách hoạt động: Resolve config từ map → return → gather → Store xa nếu cần.
        /// </summary>
        public void EnqueueIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            in AIInfluenceMapTickContext influence = default,
            int plannerTickIndex = 0)
        {
            if (snapshot == null || queue == null)
            {
                return;
            }

            AIEconomyRuntimeConfig config = AIEconomyConfigResolver.Resolve(snapshot, manualOverrides);
            GatherCommand gatherCommand = null;
            BuildBuildingCommand storeBuildCommand = config.StoreBuildCommand;

            AIConstructionAssignment.SyncReservedBuilder(snapshot.Workers);

            bool isGatherRefreshTick = manualOverrides.EnableWorkerGatherRefresh
                && AIWorkerGatherRefreshPlanner.ShouldRefreshThisTick(
                    plannerTickIndex,
                    manualOverrides.WorkerGatherRefreshIntervalTicks);

            if (isGatherRefreshTick)
            {
                AIWorkerGatherRefreshPlanner.EnqueueRefreshIntents(snapshot, queue);
            }
            else
            {
                EnqueueWorkerGatherAndReturn(snapshot, queue, config, ref gatherCommand);
            }

            EnqueueRemoteStoreBuildIfNeeded(snapshot, queue, config, influence, ref storeBuildCommand);
        }

        private void EnqueueWorkerGatherAndReturn(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIEconomyRuntimeConfig config,
            ref GatherCommand gatherCommandCache)
        {
            Vector3 anchor = GetEconomyAnchor(snapshot);
            int gatherCapable = CountGatherCapableWorkers(snapshot);

            ComputeGatherSlotTargets(
                gatherCapable,
                out int stoneSlots,
                out int woodSlots,
                out int foodSlots);
            CountActiveGatherersByKind(
                snapshot,
                config,
                out int stoneActive,
                out int woodActive,
                out int foodActive);

            CollectIdleGatherWorkers(snapshot, idleGatherWorkersScratch);
            reservedGatherSupplyThisTick.Clear();

            for (int i = 0; i < idleGatherWorkersScratch.Count; i++)
            {
                Worker worker = idleGatherWorkersScratch[i];
                if (AIConstructionAssignment.ShouldSkipGatherForWorker(worker))
                {
                    continue;
                }

                gatherCommandCache ??= ResolveGatherCommand(worker);
                if (gatherCommandCache == null)
                {
                    continue;
                }

                if (!AIWorkerCommandGuard.CanAssignGatherCommand(worker))
                {
                    continue;
                }

                SupplyKind preferredKind = PickGatherKindForDeficit(
                    stoneSlots,
                    woodSlots,
                    foodSlots,
                    stoneActive,
                    woodActive,
                    foodActive);

                if (!TryPickGatherTarget(
                        snapshot,
                        config,
                        worker,
                        preferredKind,
                        anchor,
                        stoneSlots,
                        woodSlots,
                        foodSlots,
                        stoneActive,
                        woodActive,
                        foodActive,
                        out GatherableSupply supply,
                        out Collider supplyCollider))
                {
                    continue;
                }

                if (!worker.ShouldIssueGatherTo(supply))
                {
                    continue;
                }

                if (!AIHitUtility.TryCreateHit(supplyCollider, out RaycastHit hit))
                {
                    continue;
                }

                IncrementPendingGatherCount(preferredKind, ref stoneActive, ref woodActive, ref foodActive);
                reservedGatherSupplyThisTick.Add(supply.GetInstanceID());

                queue.Enqueue(new AICommandIntent(
                    AIEconomyPriority.GatherVisibleSupply,
                    AIManagerIds.Economy,
                    worker,
                    gatherCommandCache,
                    hit,
                    mouseButton: MouseButton.Right));
            }
        }

        /// <summary>
        /// Mục tiêu: Worker rảnh cho planner gather (kể cả sau build xong, không kẹt blackboard gather cũ).
        /// Cách hoạt động: Không build/gather/return/mang tài nguyên; có GatherCommand.
        /// </summary>
        private static void CollectIdleGatherWorkers(AIWorldStateSnapshot snapshot, List<Worker> output)
        {
            output.Clear();
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (!IsEligibleForPlannerGather(worker))
                {
                    continue;
                }

                output.Add(worker);
            }

            output.Sort(static (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        }

        private static bool IsEligibleForPlannerGather(Worker worker) =>
            worker != null
            && ResolveGatherCommand(worker) != null
            && !worker.IsCommittedToConstructionWork
            && !worker.IsGatheringOrReturning
            && !worker.HasSupplies;

        private static int CountGatherCapableWorkers(AIWorldStateSnapshot snapshot)
        {
            int count = 0;
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker != null && ResolveGatherCommand(worker) != null)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Mục tiêu: Đếm worker đang gắn với từng loại mỏ (theo node BT đang khai thác).
        /// Cách hoạt động: TryGetCommittedGatherSupply + ClassifySupply.
        /// </summary>
        private static void CountActiveGatherersByKind(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            out int stoneCount,
            out int woodCount,
            out int foodCount)
        {
            stoneCount = woodCount = foodCount = 0;
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker == null || !worker.TryGetCommittedGatherSupply(out GatherableSupply supply))
                {
                    continue;
                }

                if (supply == null || supply.Amount <= 0)
                {
                    continue;
                }

                switch (ClassifySupply(config, supply.Supply))
                {
                    case SupplyKind.Stone:
                        stoneCount++;
                        break;
                    case SupplyKind.Wood:
                        woodCount++;
                        break;
                    case SupplyKind.Food:
                        foodCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Gán worker rảnh vào loại mỏ còn thiếu slot (không theo InstanceID cố định).
        /// Cách hoạt động: deficit = slot − (active + pending); ưu tiên Food → Wood → Stone khi hòa.
        /// </summary>
        private static SupplyKind PickGatherKindForDeficit(
            int stoneSlots,
            int woodSlots,
            int foodSlots,
            int stoneCount,
            int woodCount,
            int foodCount)
        {
            int stoneDeficit = stoneSlots - stoneCount;
            int woodDeficit = woodSlots - woodCount;
            int foodDeficit = foodSlots - foodCount;

            if (foodDeficit > 0 && foodDeficit >= woodDeficit && foodDeficit >= stoneDeficit)
            {
                return SupplyKind.Food;
            }

            if (woodDeficit > 0 && woodDeficit >= stoneDeficit)
            {
                return SupplyKind.Wood;
            }

            if (stoneDeficit > 0)
            {
                return SupplyKind.Stone;
            }

            int stoneOver = stoneCount - stoneSlots;
            int woodOver = woodCount - woodSlots;
            int foodOver = foodCount - foodSlots;
            if (foodOver <= woodOver && foodOver <= stoneOver)
            {
                return SupplyKind.Food;
            }

            if (woodOver <= stoneOver)
            {
                return SupplyKind.Wood;
            }

            return SupplyKind.Stone;
        }

        private static void IncrementPendingGatherCount(
            SupplyKind kind,
            ref int stoneCount,
            ref int woodCount,
            ref int foodCount)
        {
            switch (kind)
            {
                case SupplyKind.Stone:
                    stoneCount++;
                    break;
                case SupplyKind.Wood:
                    woodCount++;
                    break;
                case SupplyKind.Food:
                    foodCount++;
                    break;
            }
        }

        /// <summary>
        /// Mục tiêu: Chia slot gather 1:1:1; phần dư ưu tiên Stone rồi Wood, Food cuối.
        /// Cách hoạt động: n/3 mỗi loại; remainder +1 Stone, +1 Wood (nếu có), không cộng Food trước khi đủ đá/gỗ.
        /// </summary>
        private static void ComputeGatherSlotTargets(
            int gatherWorkerCount,
            out int stoneSlots,
            out int woodSlots,
            out int foodSlots)
        {
            if (gatherWorkerCount <= 0)
            {
                stoneSlots = woodSlots = foodSlots = 0;
                return;
            }

            stoneSlots = gatherWorkerCount / 3;
            woodSlots = gatherWorkerCount / 3;
            foodSlots = gatherWorkerCount / 3;
            int remainder = gatherWorkerCount % 3;
            if (remainder >= 1)
            {
                stoneSlots++;
            }

            if (remainder >= 2)
            {
                woodSlots++;
            }
        }

        private bool TryPickGatherTarget(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            Worker worker,
            SupplyKind preferredKind,
            Vector3 anchor,
            int stoneSlots,
            int woodSlots,
            int foodSlots,
            int stoneCount,
            int woodCount,
            int foodCount,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            if (TryPickGatherTargetForKind(snapshot, config, worker, preferredKind, anchor, out bestSupply, out bestCollider))
            {
                return true;
            }

            if (TryPickGatherTargetForDeficitKind(
                    snapshot,
                    config,
                    worker,
                    preferredKind,
                    anchor,
                    stoneSlots,
                    woodSlots,
                    foodSlots,
                    stoneCount,
                    woodCount,
                    foodCount,
                    out bestSupply,
                    out bestCollider))
            {
                return true;
            }

            bestSupply = null;
            bestCollider = null;
            return false;
        }

        /// <summary>
        /// Mục tiêu: Fallback chỉ sang loại còn thiếu slot — tránh worker mới luôn nhảy sang đá.
        /// Cách hoạt động: Thử Food → Wood → Stone nếu còn deficit; bỏ qua preferred và loại đã đủ quota.
        /// </summary>
        private bool TryPickGatherTargetForDeficitKind(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            Worker worker,
            SupplyKind preferredKind,
            Vector3 anchor,
            int stoneSlots,
            int woodSlots,
            int foodSlots,
            int stoneCount,
            int woodCount,
            int foodCount,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            if (preferredKind != SupplyKind.Food
                && foodSlots - foodCount > 0
                && TryPickGatherTargetForKind(snapshot, config, worker, SupplyKind.Food, anchor, out bestSupply, out bestCollider))
            {
                return true;
            }

            if (preferredKind != SupplyKind.Wood
                && woodSlots - woodCount > 0
                && TryPickGatherTargetForKind(snapshot, config, worker, SupplyKind.Wood, anchor, out bestSupply, out bestCollider))
            {
                return true;
            }

            if (preferredKind != SupplyKind.Stone
                && stoneSlots - stoneCount > 0
                && TryPickGatherTargetForKind(snapshot, config, worker, SupplyKind.Stone, anchor, out bestSupply, out bestCollider))
            {
                return true;
            }

            bestSupply = null;
            bestCollider = null;
            return false;
        }

        /// <summary>
        /// Mục tiêu: Mỏ gần nhất của một loại supply (visible).
        /// Cách hoạt động: Lọc theo kind; score = khoảng cách worker + anchor có trọng số.
        /// </summary>
        private bool TryPickGatherTargetForKind(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            Worker worker,
            SupplyKind kind,
            Vector3 anchor,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            bestSupply = null;
            bestCollider = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply supply = snapshot.GatherableSupplies[i];
                if (supply == null || supply.Amount <= 0 || !supply.IsVisible)
                {
                    continue;
                }

                if (reservedGatherSupplyThisTick.Contains(supply.GetInstanceID()))
                {
                    continue;
                }

                if (ClassifySupply(config, supply.Supply) != kind)
                {
                    continue;
                }

                Collider collider = supply.GetComponent<Collider>();
                if (collider == null)
                {
                    collider = supply.GetComponentInChildren<Collider>();
                }

                if (collider == null)
                {
                    continue;
                }

                float distWorker = (supply.transform.position - worker.transform.position).sqrMagnitude;
                float distAnchor = (supply.transform.position - anchor).sqrMagnitude;
                float score = distWorker + distAnchor * config.AnchorDistanceWeight;

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestSupply = supply;
                bestCollider = collider;
            }

            return bestSupply != null;
        }

        private void EnqueueRemoteStoreBuildIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIEconomyRuntimeConfig config,
            in AIInfluenceMapTickContext influence,
            ref BuildBuildingCommand storeBuildCommandCache)
        {
            if (snapshot.CivilCentral == null)
            {
                return;
            }

            if (!AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.StoreHouseDisplayName))
            {
                return;
            }

            if (!TryGetRemoteClusterCenter(snapshot, config, out Vector3 clusterCenter))
            {
                return;
            }

            storeBuildCommandCache ??= config.StoreBuildCommand;
            if (storeBuildCommandCache == null)
            {
                return;
            }

            if (!SupplyAffordability.HasEnough(snapshot.Owner, storeBuildCommandCache.Building.Cost))
            {
                return;
            }

            CommandContext lockContext = new(
                snapshot.Owner,
                snapshot.CivilCentral,
                AIHitUtility.AtPoint(snapshot.CivilCentral.transform.position));
            if (storeBuildCommandCache.IsLocked(lockContext))
            {
                return;
            }

            CollectDepositTypesFromBuilding(storeBuildCommandCache.Building);
            if (SupplyDepositLocator.TryFindClosest(
                    clusterCenter,
                    config.StoreCoverageRadius,
                    snapshot.Owner,
                    depositTypeScratch,
                    out _))
            {
                return;
            }

            if (!TryFindIdleWorker(snapshot, out Worker builder))
            {
                return;
            }

            if (!TryFindStorePlacement(
                    config,
                    storeBuildCommandCache,
                    clusterCenter,
                    snapshot.CivilCentral.transform.position,
                    influence,
                    out Vector3 placement))
            {
                return;
            }

            RaycastHit hit = AIHitUtility.AtPoint(placement);
            queue.Enqueue(new AICommandIntent(
                AIEconomyPriority.BuildRemoteStore,
                AIManagerIds.Economy,
                builder,
                storeBuildCommandCache,
                hit,
                mouseButton: MouseButton.Left));

            if (storeBuildCommandCache.Building != null)
            {
                AIInfraBuildOrderTracker.RegisterOrder(
                    snapshot.Owner,
                    storeBuildCommandCache.Building.Name,
                    builder.GetInstanceID());
            }
        }

        /// <summary>
        /// Mục tiêu: Cụm mỏ xa nhà chính — ưu tiên Store giảm haul.
        /// Cách hoạt động: Lọc supply visible cách CC &gt; ngưỡng; trung bình vị trí cụm.
        /// </summary>
        private bool TryGetRemoteClusterCenter(AIWorldStateSnapshot snapshot, AIEconomyRuntimeConfig config, out Vector3 center)
        {
            center = default;
            if (config.RemoteClusterMinDistance >= float.MaxValue * 0.5f)
            {
                return false;
            }

            Vector3 ccPosition = snapshot.CivilCentral.transform.position;
            float minSqr = config.RemoteClusterMinDistance * config.RemoteClusterMinDistance;
            int count = 0;
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < snapshot.GatherableSupplies.Count; i++)
            {
                GatherableSupply supply = snapshot.GatherableSupplies[i];
                if (supply == null || supply.Amount <= 0 || !supply.IsVisible)
                {
                    continue;
                }

                float sqr = (supply.transform.position - ccPosition).sqrMagnitude;
                if (sqr < minSqr)
                {
                    continue;
                }

                sum += supply.transform.position;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            center = sum / count;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Store gần cụm mỏ xa (hướng về base); hợp lệ chỉ qua Restrictions trên lệnh build.
        /// Cách hoạt động: Seed cluster→CC; quét vòng quanh seed rồi fallback Civil Central.
        /// </summary>
        private static bool TryFindStorePlacement(
            AIEconomyRuntimeConfig config,
            BuildBuildingCommand storeCommand,
            Vector3 clusterCenter,
            Vector3 civilCentralPosition,
            in AIInfluenceMapTickContext influence,
            out Vector3 placement)
        {
            Vector3 towardBase = civilCentralPosition - clusterCenter;
            towardBase.y = 0f;
            if (towardBase.sqrMagnitude < 0.01f)
            {
                towardBase = Vector3.forward;
            }

            Vector3 seed = clusterCenter + towardBase.normalized * config.StoreOffsetFromCluster;

            if (AIInfluencePlacementUtility.TryFindPlacement(
                    storeCommand,
                    seed,
                    config.PlacementSearchRings,
                    config.PlacementSearchStep,
                    expandSearch: false,
                    preferNearAnchor: false,
                    influence,
                    out placement))
            {
                return true;
            }

            return AIInfluencePlacementUtility.TryFindPlacement(
                storeCommand,
                civilCentralPosition,
                config.PlacementSearchRings,
                config.PlacementSearchStep,
                expandSearch: true,
                preferNearAnchor: true,
                influence,
                out placement);
        }

        private static bool TryFindIdleWorker(AIWorldStateSnapshot snapshot, out Worker worker)
        {
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker candidate = snapshot.Workers[i];
                if (AIWorkerCommandGuard.CanAssignCommand(candidate))
                {
                    worker = candidate;
                    return true;
                }
            }

            worker = null;
            return false;
        }

        private Vector3 GetEconomyAnchor(AIWorldStateSnapshot snapshot) =>
            snapshot.CivilCentral != null
                ? snapshot.CivilCentral.transform.position
                : snapshot.Workers.Count > 0
                    ? snapshot.Workers[0].transform.position
                    : Vector3.zero;

        private static SupplyKind ClassifySupply(AIEconomyRuntimeConfig config, SupplySO supply)
        {
            if (supply == null)
            {
                return SupplyKind.Unknown;
            }

            if (config.StoneSupply != null && supply.Equals(config.StoneSupply))
            {
                return SupplyKind.Stone;
            }

            if (config.WoodSupply != null && supply.Equals(config.WoodSupply))
            {
                return SupplyKind.Wood;
            }

            if (config.FoodSupply != null && supply.Equals(config.FoodSupply))
            {
                return SupplyKind.Food;
            }

            string name = supply.name;
            if (name.Contains("Stone", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Stone;
            }

            if (name.Contains("Wood", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Wood;
            }

            if (name.Contains("Food", System.StringComparison.OrdinalIgnoreCase))
            {
                return SupplyKind.Food;
            }

            return SupplyKind.Unknown;
        }

        private void CollectDepositTypesFromBuilding(BuildingSO building)
        {
            depositTypeScratch.Clear();
            if (building != null)
            {
                depositTypeScratch.Add(building);
            }
        }

        private static GatherCommand ResolveGatherCommand(Worker worker)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is GatherCommand gather)
                {
                    return gather;
                }
            }

            return null;
        }

        private static StopCommand ResolveStopCommand(Worker worker)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(worker);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is StopCommand stop)
                {
                    return stop;
                }
            }

            return null;
        }

        private enum SupplyKind
        {
            Unknown,
            Stone,
            Wood,
            Food
        }
    }

    /// <summary>
    /// Supply/command SO trên <see cref="AIController"/> — gán Inspector là dùng; để trống thì quét map/prefab.
    /// Khoảng cách, placement rings, v.v. luôn tự tính trong <see cref="AIEconomyConfigResolver"/>.
    /// </summary>
    [System.Serializable]
    public sealed class AIEconomySettings
    {
        [Header("Command & supply assets (Inspector)")]
        [Tooltip("Để trống = suy từ GatherableSupply trên map.")]
        [SerializeField] private SupplySO stoneSupply;
        [SerializeField] private SupplySO woodSupply;
        [SerializeField] private SupplySO foodSupply;
        [Tooltip("Để trống = tự tìm Build Store trên worker.")]
        [SerializeField] private BuildBuildingCommand storeBuildCommand;

        [Header("Gather refresh (mỗi N tick planner)")]
        [Tooltip("Bật: tick 10, 20, … gửi Stop + Move ngắn cho worker đang gather; tick đó không gán mỏ mới.")]
        [SerializeField] private bool enableWorkerGatherRefresh = true;
        [Tooltip("Số tick AI giữa mỗi lần reset gather (AIController tick, không phải frame).")]
        [SerializeField] private int workerGatherRefreshIntervalTicks = 10;

        public SupplySO StoneSupply => stoneSupply;
        public SupplySO WoodSupply => woodSupply;
        public SupplySO FoodSupply => foodSupply;
        public BuildBuildingCommand StoreBuildCommand => storeBuildCommand;
        public bool EnableWorkerGatherRefresh => enableWorkerGatherRefresh;
        public int WorkerGatherRefreshIntervalTicks => Mathf.Max(1, workerGatherRefreshIntervalTicks);

        public static AIEconomySettings Default => new();
    }
}
