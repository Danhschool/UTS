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
    /// SRP: Worker gather 40/40/20 hoặc 60% thiếu; hết mỏ food → 70/30 đá-gỗ; thiếu food → thêm Corral; Store xa CC.
    /// Mỗi worker: <see cref="GatherCommand"/> → BT Gather Sub Graph tự loop + tự return khi đầy (Petra: không spam ReturnSupplies).
    /// Chỉ mỏ <see cref="GatherableSupply.IsVisible"/> (fog gameplay chung với người chơi); khóa gather trên <see cref="Worker.ShouldIssueGatherTo"/>.
    /// </summary>
    public sealed class AIEconomyManager
    {
        private readonly AIEconomySettings manualOverrides;
        private readonly List<AbstractUnitSO> depositTypeScratch = new(4);
        private readonly List<Worker> idleGatherWorkersScratch = new(32);
        private readonly HashSet<int> reservedGatherSupplyThisTick = new(32);
        private readonly AIEconomyGatherSupplyIndex gatherSupplyIndex = new();

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
            int plannerTickIndex = 0,
            AIEconomyRuntimeConfig? preResolvedConfig = null)
        {
            if (snapshot == null || queue == null)
            {
                return;
            }

            AIEconomyRuntimeConfig config = preResolvedConfig
                ?? AIEconomyConfigResolver.Resolve(snapshot, manualOverrides);
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

            EnqueueWorkerGatherAndReturn(snapshot, queue, config, plannerTickIndex, ref gatherCommand);

            AIEconomyCorralPlanner.TryEnqueueExtraCorral(
                snapshot,
                queue,
                config,
                manualOverrides,
                influence,
                plannerTickIndex);

            EnqueueRemoteStoreBuildIfNeeded(snapshot, queue, config, influence, ref storeBuildCommand);

            bool hasFoodMines = AIEconomyFoodGatherUtility.HasVisibleFoodGatherNode(snapshot, config);
            if (!ShouldSkipWildFoodWhenNoMines(hasFoodMines))
            {
                AIEconomyWildFoodPlanner.EnqueueHuntIntents(
                    snapshot,
                    queue,
                    config,
                    manualOverrides,
                    GetEconomyAnchor(snapshot));
            }
        }

        private bool ShouldSkipWildFoodWhenNoMines(bool hasFoodMines) =>
            manualOverrides.EnableStoneWoodOnlyWhenNoFoodMines && !hasFoodMines;

        private void EnqueueWorkerGatherAndReturn(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIEconomyRuntimeConfig config,
            int plannerTickIndex,
            ref GatherCommand gatherCommandCache)
        {
            Vector3 anchor = GetEconomyAnchor(snapshot);
            int gatherCapable = CountGatherCapableWorkers(snapshot);
            bool hasVisibleFoodMines = AIEconomyFoodGatherUtility.HasVisibleFoodGatherNode(snapshot, config);
            gatherSupplyIndex.Rebuild(snapshot, config);

            AIWorkerGatherSlotPlanner.ComputeGatherSlotTargets(
                snapshot.Owner,
                plannerTickIndex,
                snapshot,
                manualOverrides,
                gatherCapable,
                hasVisibleFoodMines,
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

                if (worker.HasStaleGatherCommand)
                {
                    worker.InterruptGatherWorkCycle();
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
                        hasVisibleFoodMines,
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

                SupplyKind assignedKind = ClassifySupply(config, supply.Supply);
                IncrementPendingGatherCount(assignedKind, ref stoneActive, ref woodActive, ref foodActive);
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
        /// Mục tiêu: Gán worker rảnh vào loại mỏ còn thiếu slot (40/40/20 hoặc 60% thiếu + 20% mỗi loại kia).
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

            SupplyKind bestKind = SupplyKind.Stone;
            int bestDeficit = stoneDeficit;

            if (woodDeficit > bestDeficit)
            {
                bestKind = SupplyKind.Wood;
                bestDeficit = woodDeficit;
            }

            if (foodSlots > 0 && foodDeficit > bestDeficit)
            {
                bestKind = SupplyKind.Food;
                bestDeficit = foodDeficit;
            }

            if (bestDeficit > 0)
            {
                return bestKind;
            }

            int stoneOver = stoneCount - stoneSlots;
            int woodOver = woodCount - woodSlots;
            int foodOver = foodCount - foodSlots;
            if (foodSlots > 0 && foodOver <= woodOver && foodOver <= stoneOver)
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

        private bool TryPickGatherTarget(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            Worker worker,
            SupplyKind preferredKind,
            Vector3 anchor,
            bool hasVisibleFoodMines,
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
                    hasVisibleFoodMines,
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

            return TryPickAnyVisibleStoneOrWood(
                snapshot,
                config,
                worker,
                anchor,
                out bestSupply,
                out bestCollider);
        }

        /// <summary>
        /// Mục tiêu: Fallback khi quota/loại ưu tiên không tìm được mỏ — vẫn khai thác đá/gỗ visible.
        /// Cách hoạt động: Thử Stone rồi Wood, bỏ qua slot deficit.
        /// </summary>
        private bool TryPickAnyVisibleStoneOrWood(
            AIWorldStateSnapshot snapshot,
            AIEconomyRuntimeConfig config,
            Worker worker,
            Vector3 anchor,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            bool woodFirst = snapshot.Wood < snapshot.Stone;
            SupplyKind first = woodFirst ? SupplyKind.Wood : SupplyKind.Stone;
            SupplyKind second = woodFirst ? SupplyKind.Stone : SupplyKind.Wood;

            if (TryPickGatherTargetForKind(snapshot, config, worker, first, anchor, out bestSupply, out bestCollider))
            {
                return true;
            }

            return TryPickGatherTargetForKind(snapshot, config, worker, second, anchor, out bestSupply, out bestCollider);
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
            bool hasVisibleFoodMines,
            int stoneSlots,
            int woodSlots,
            int foodSlots,
            int stoneCount,
            int woodCount,
            int foodCount,
            out GatherableSupply bestSupply,
            out Collider bestCollider)
        {
            if (hasVisibleFoodMines
                && preferredKind != SupplyKind.Food
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
            bool excludeCorpseFood = kind == SupplyKind.Food;
            return gatherSupplyIndex.TryPickClosest(
                ToIndexKind(kind),
                worker,
                anchor,
                config.AnchorDistanceWeight,
                excludeCorpseFood,
                reservedGatherSupplyThisTick,
                out bestSupply,
                out bestCollider);
        }

        static AIEconomyGatherSupplyIndex.SupplyKind ToIndexKind(SupplyKind kind) =>
            kind switch
            {
                SupplyKind.Stone => AIEconomyGatherSupplyIndex.SupplyKind.Stone,
                SupplyKind.Wood => AIEconomyGatherSupplyIndex.SupplyKind.Wood,
                SupplyKind.Food => AIEconomyGatherSupplyIndex.SupplyKind.Food,
                _ => AIEconomyGatherSupplyIndex.SupplyKind.Unknown
            };

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

            Vector3 ccPosition = snapshot.CivilCentral.transform.position;
            if (!TryFindStorePlacement(
                    config,
                    storeBuildCommandCache,
                    clusterCenter,
                    ccPosition,
                    snapshot.Owner,
                    influence,
                    out Vector3 placement))
            {
                return;
            }

            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(ccPosition, influence);
            PlacementFieldSelectionRegistry.RegisterSelectedPlacement(snapshot.Owner, placement, fieldGrid);

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
                if (supply == null
                    || supply.Amount <= 0
                    || !FactionFogQuery.IsVisibleTo(snapshot.Owner, supply))
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
            Owner placementOwner,
            in AIInfluenceMapTickContext influence,
            out Vector3 placement)
        {
            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(
                civilCentralPosition,
                influence);

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
                    placementOwner,
                    fieldGrid,
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
                placementOwner,
                fieldGrid,
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

        private static SupplyKind ClassifySupply(AIEconomyRuntimeConfig config, SupplySO supply) =>
            ToManagerSupplyKind(AIEconomySupplyKindClassifier.Classify(
                supply,
                config.StoneSupply,
                config.WoodSupply,
                config.FoodSupply));

        static SupplyKind ToManagerSupplyKind(AIEconomySupplyKindClassifier.Kind kind) =>
            kind switch
            {
                AIEconomySupplyKindClassifier.Kind.Stone => SupplyKind.Stone,
                AIEconomySupplyKindClassifier.Kind.Wood => SupplyKind.Wood,
                AIEconomySupplyKindClassifier.Kind.Food => SupplyKind.Food,
                _ => SupplyKind.Unknown
            };

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

        [Header("Gather — chia worker & cân kho")]
        [Tooltip("Mỗi N tick planner mới đọc Stone/Wood/Food và quyết định cân bằng hay 60% sang loại thiếu.")]
        [SerializeField] private int gatherBalanceCheckIntervalTicks = 20;
        [Tooltip("Lệch tối đa giữa loại nhiều nhất và ít nhất (≥3 = mất cân bằng).")]
        [SerializeField] private float gatherImbalanceMaxRatio = 3f;

        [Header("Gather refresh (mỗi N tick planner)")]
        [Tooltip("Bật: tick 10, 20, … gửi Stop + Move ngắn cho worker đang gather; tick đó không gán mỏ mới.")]
        [SerializeField] private bool enableWorkerGatherRefresh = true;
        [Tooltip("Số tick AI giữa mỗi lần reset gather (AIController tick, không phải frame).")]
        [SerializeField] private int workerGatherRefreshIntervalTicks = 18;

        [Header("Food — Corral bổ sung")]
        [Tooltip("Bật: kho food thấp → economy enqueue thêm Corral (vẫn ưu tiên gather mỏ food nếu còn).")]
        [SerializeField] private bool enableExtraCorralWhenFoodLow = true;
        [SerializeField] private int foodLowAmountForExtraCorral = 80;
        [Tooltip("Tổng số Corral tối đa (gồm Corral đầu từ build order).")]
        [SerializeField] private int maxCorralsForFoodEconomy = 4;
        [Tooltip("Tối thiểu số tick planner giữa hai lần thử xây Corral bổ sung.")]
        [SerializeField] private int extraCorralAttemptIntervalTicks = 40;

        [Header("Food — hết mỏ gather")]
        [Tooltip("Bật: không còn mỏ food visible → chỉ đá/gỗ 7:3, không săn thú.")]
        [SerializeField] private bool enableStoneWoodOnlyWhenNoFoodMines = true;
        [SerializeField, Range(0.51f, 0.95f)] private float stoneWoodMajorShare = 0.7f;
        [SerializeField, Min(1)] private int stoneWoodMinMajorSlots = 7;

        [Header("Food — không có mỏ (săn thú)")]
        [Tooltip("Bật: kho food thấp và không thấy mỏ food → worker Attack WildAnimal.")]
        [SerializeField] private bool enableWildFoodHunt = true;
        [SerializeField] private int foodHuntBelowAmount = 80;
        [Tooltip("0 = không giới hạn khoảng cách săn.")]
        [SerializeField] private float wildFoodHuntMaxDistance = 120f;
        [SerializeField] private int maxWildFoodHuntersPerTick = 3;

        public SupplySO StoneSupply => stoneSupply;
        public SupplySO WoodSupply => woodSupply;
        public SupplySO FoodSupply => foodSupply;
        public BuildBuildingCommand StoreBuildCommand => storeBuildCommand;
        public int GatherBalanceCheckIntervalTicks => Mathf.Max(1, gatherBalanceCheckIntervalTicks);
        public float GatherImbalanceMaxRatio => Mathf.Max(1.1f, gatherImbalanceMaxRatio);
        public bool EnableWorkerGatherRefresh => enableWorkerGatherRefresh;
        public int WorkerGatherRefreshIntervalTicks => Mathf.Max(1, workerGatherRefreshIntervalTicks);
        public bool EnableExtraCorralWhenFoodLow => enableExtraCorralWhenFoodLow;
        public int FoodLowAmountForExtraCorral => Mathf.Max(0, foodLowAmountForExtraCorral);
        public int MaxCorralsForFoodEconomy => Mathf.Max(1, maxCorralsForFoodEconomy);
        public int ExtraCorralAttemptIntervalTicks => Mathf.Max(1, extraCorralAttemptIntervalTicks);
        public bool EnableStoneWoodOnlyWhenNoFoodMines => enableStoneWoodOnlyWhenNoFoodMines;
        public float StoneWoodMajorShare => Mathf.Clamp(stoneWoodMajorShare, 0.51f, 0.95f);
        public int StoneWoodMinMajorSlots => Mathf.Max(1, stoneWoodMinMajorSlots);
        public bool EnableWildFoodHunt => enableWildFoodHunt;
        public int FoodHuntBelowAmount => Mathf.Max(0, foodHuntBelowAmount);
        public float WildFoodHuntMaxDistance => Mathf.Max(0f, wildFoodHuntMaxDistance);
        public int MaxWildFoodHuntersPerTick => Mathf.Max(1, maxWildFoodHuntersPerTick);

        public static AIEconomySettings Default => new();
    }
}
