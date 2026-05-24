using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Train Barrack, phòng thủ Civil Central, hàng Defense Tower, attack wave tới CC địch.
    /// </summary>
    public sealed class AIMilitaryManager
    {
        private const int MaxDefenseAssignmentsPerTick = 8;
        private const int TowerLineSlotCount = 8;

        private readonly AIMilitarySettings manualOverrides;
        private readonly AIBaseSettings baseSettings;
        private readonly MinimapFogSystemReference fogSystemReference;
        private readonly List<BaseBuilding> barrackScratch = new(4);
        private readonly List<BuildUnitCommand> trainCommandScratch = new(8);
        private readonly List<IDamageable> threatScratch = new(32);
        private readonly List<AbstractUnit> militaryScratch = new(48);
        private readonly List<Vector3> threatPositionScratch = new(32);
        private readonly HashSet<int> assignedMilitaryThisTick = new(64);
        private readonly List<AbstractUnit> rallyArmyScratch = new(48);
        private readonly List<AbstractUnit> rallyMoveFallbackScratch = new(48);
        private readonly List<AbstractUnit> unifiedArmyScratch = new(48);
        private int patrolPhase;

        public AIMilitaryManager(
            AIMilitarySettings militaryOverrides = null,
            AIBaseSettings baseOverrides = null,
            MinimapFogSystemReference fogSystem = null)
        {
            manualOverrides = militaryOverrides ?? AIMilitarySettings.Default;
            baseSettings = baseOverrides ?? AIBaseSettings.Default;
            fogSystemReference = fogSystem;
        }

        /// <summary>
        /// Mục tiêu: Sinh intent quân sự (defense → train → tower line → attack wave).
        /// Cách hoạt động: Resolve config; ưu tiên phòng thủ CC; sau đó macro offense.
        /// </summary>
        public void EnqueueIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            in AIInfluenceMapTickContext influence = default,
            List<Vector3> sharedThreatPositions = null,
            AIDifficultyRuntimeOverlay difficulty = default)
        {
            if (snapshot == null || queue == null || snapshot.CivilCentral == null)
            {
                return;
            }

            AIMilitaryRuntimeConfig config = AIMilitaryConfigResolver.Resolve(
                snapshot,
                manualOverrides,
                baseSettings,
                difficulty);
            Vector3 ccPosition = snapshot.CivilCentral.transform.position;

            SyncEnemyCivilCentralMemory(snapshot, config);
            CollectMilitaryUnits(snapshot, militaryScratch);
            assignedMilitaryThisTick.Clear();

            int threatCount = AIMilitaryHostileScanner.CollectThreatsNearCivilCentral(
                snapshot.Owner,
                ccPosition,
                config.DefenseRadius,
                config.RequireVisibleTargets,
                threatScratch);

            if (sharedThreatPositions != null && sharedThreatPositions.Count > 0)
            {
                threatPositionScratch.Clear();
                threatPositionScratch.AddRange(sharedThreatPositions);
            }
            else
            {
                AIInfluenceMap.CollectThreatPositionsFromSnapshot(
                    snapshot,
                    snapshot.Owner,
                    config.DefenseRadius * 1.5f,
                    config.RequireVisibleTargets,
                    threatPositionScratch);
            }

            bool hasRallyTarget = AIMilitaryRallyPlanner.TryResolveRallyAttackTarget(
                snapshot.Owner,
                config.EnemyOwner,
                config.RequireVisibleTargets,
                ccPosition,
                out _,
                out _);
            bool rallySessionActive = AIMilitaryRallySessionTracker.GetPhase(snapshot.Owner)
                != AIMilitaryRallySessionTracker.RallyPhase.Idle;

            if (manualOverrides.EnablePostContactBufferedOffense)
            {
                AIMilitaryPostContactOffensePlanner.SyncPostContactState(
                    snapshot.Owner,
                    manualOverrides.PostContactExploreBuildSeconds,
                    hasRallyTarget,
                    rallySessionActive,
                    threatCount > 0);
            }

            bool inExploreBuildBuffer = manualOverrides.EnablePostContactBufferedOffense
                && AIMilitaryPostContactOffensePlanner.IsInExploreBuildBuffer(snapshot.Owner);
            bool postBufferOffenseReady = manualOverrides.EnablePostContactBufferedOffense
                && AIMilitaryPostContactOffensePlanner.ShouldExecutePostBufferOffensive(snapshot.Owner);

            bool defenseRingMode = manualOverrides.EnableDefenseRingExpansion;
            float operationalRadius = AIMilitaryOperationalLeash.GetOperationalRadius(
                manualOverrides,
                snapshot.Owner);
            AIBaseRuntimeConfig baseRuntimeConfig = AIBaseConfigResolver.Resolve(
                snapshot,
                baseSettings,
                difficulty);

            if (defenseRingMode)
            {
                AIMilitaryDefenseRingBuildCoordinator.EnqueueEconomyRecoveryIfNeeded(
                    snapshot,
                    queue,
                    baseRuntimeConfig,
                    baseSettings,
                    manualOverrides,
                    ccPosition);
            }

            bool armyRalliedOnContact = false;
            bool ralliedFullArmy = true;
            int defenseRingThreatsInLeash = 0;
            if (defenseRingMode)
            {
                defenseRingThreatsInLeash = AIMilitaryHostileScanner.CollectThreatsNearCivilCentral(
                    snapshot.Owner,
                    ccPosition,
                    operationalRadius,
                    config.RequireVisibleTargets,
                    threatScratch);
                if (defenseRingThreatsInLeash > 0)
                {
                    AIMilitaryDefenseRingCombatUtility.EnqueueAttacksWithinOperationalZone(
                        snapshot,
                        queue,
                        snapshot.Owner,
                        ccPosition,
                        operationalRadius,
                        config.RequireVisibleTargets,
                        militaryScratch,
                        assignedMilitaryThisTick,
                        MaxDefenseAssignmentsPerTick);
                }
            }
            else if (threatCount > 0)
            {
                EnqueueDefenseIntents(snapshot, queue, config, ccPosition);
            }

            if (!armyRalliedOnContact
                && !inExploreBuildBuffer
                && TryEnqueueArmyRallyOnHostileContact(
                    snapshot,
                    config,
                    ccPosition,
                    defenseRingMode,
                    operationalRadius,
                    out ralliedFullArmy))
            {
                armyRalliedOnContact = true;
            }
            else if (postBufferOffenseReady
                     && (!defenseRingMode
                         || !manualOverrides.RequireEnemyCcInLeashForUnifiedAttack
                         || AIMilitaryOperationalLeash.IsEnemyCivilCentralInOperationalZone(
                             snapshot.Owner,
                             config.EnemyOwner,
                             ccPosition,
                             operationalRadius,
                             config.RequireVisibleTargets))
                     && TryEnqueuePostContactFullOffensive(snapshot, config, ccPosition))
            {
                ralliedFullArmy = true;
            }
            else if (!inExploreBuildBuffer)
            {
                EnqueueTowerBuildBeforeBarrackTraining(
                    snapshot,
                    queue,
                    config,
                    ccPosition,
                    influence,
                    defenseRingMode);

                if (!ShouldDeferBarrackTrainingForTower(snapshot, defenseRingMode))
                {
                    EnqueueBarrackTrainIfNeeded(snapshot, queue, config);
                }

                if (ShouldEnqueueUnifiedArmyOffense(
                        snapshot,
                        config,
                        ccPosition,
                        operationalRadius,
                        defenseRingMode))
                {
                    EnqueueUnifiedArmyOffense(snapshot, config, ccPosition);
                }
            }
            else
            {
                EnqueueTowerBuildBeforeBarrackTraining(
                    snapshot,
                    queue,
                    config,
                    ccPosition,
                    influence,
                    defenseRingMode);

                if (!ShouldDeferBarrackTrainingForTower(snapshot, defenseRingMode))
                {
                    EnqueueBarrackTrainIfNeeded(snapshot, queue, config);
                }
            }

            if (defenseRingMode)
            {
                AIMilitaryDefenseRingCombatUtility.EnqueueStopForUnitsOutsideOperationalZone(
                    snapshot,
                    queue,
                    ccPosition,
                    operationalRadius,
                    militaryScratch,
                    assignedMilitaryThisTick);
            }

            if (ShouldEnqueueLoosePatrol(
                    defenseRingMode,
                    defenseRingThreatsInLeash,
                    armyRalliedOnContact,
                    ralliedFullArmy,
                    inExploreBuildBuffer))
            {
                EnqueueLoosePatrol(snapshot, queue, ccPosition, operationalRadius, defenseRingMode);
            }
        }

        private static bool ShouldEnqueueLoosePatrol(
            bool defenseRingMode,
            int defenseRingThreatsInLeash,
            bool armyRalliedOnContact,
            bool ralliedFullArmy,
            bool inExploreBuildBuffer)
        {
            if (inExploreBuildBuffer)
            {
                return false;
            }

            if (defenseRingMode)
            {
                return defenseRingThreatsInLeash <= 0;
            }

            return !armyRalliedOnContact || !ralliedFullArmy;
        }

        private bool ShouldEnqueueUnifiedArmyOffense(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            float operationalRadius,
            bool defenseRingMode)
        {
            if (!config.EnableAttack)
            {
                return false;
            }

            bool hasArmy = AIMilitaryOperationalLeash.CountAliveMilitary(snapshot) >= config.MinArmyBeforeAttack
                || AIMilitaryExpansionPlanner.IsExpansionPhase(snapshot, manualOverrides);
            if (!hasArmy)
            {
                return false;
            }

            if (defenseRingMode && manualOverrides.RequireEnemyCcInLeashForUnifiedAttack)
            {
                return AIMilitaryOperationalLeash.IsEnemyCivilCentralInOperationalZone(
                    snapshot.Owner,
                    config.EnemyOwner,
                    ccPosition,
                    operationalRadius,
                    config.RequireVisibleTargets);
            }

            return AIMilitaryExpansionPlanner.IsExpansionPhase(snapshot, manualOverrides)
                || snapshot.MilitaryUnits.Count >= config.MinArmyBeforeAttack;
        }

        /// <summary>
        /// Mục tiêu: Thấy địch — tập hợp xa gần nhà → phase Attacking = lệnh Attack (ưu tiên lính địch).
        /// Cách hoạt động: Regroup gần CC ta → đủ quân hoặc 1 lệnh lùi → AttackCommand; Move chỉ fallback.
        /// </summary>
        private bool TryEnqueueArmyRallyOnHostileContact(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            bool defenseRingMode,
            float operationalRadius,
            out bool ralliedFullArmy)
        {
            ralliedFullArmy = true;
            if (manualOverrides.EnablePostContactBufferedOffense
                && AIMilitaryPostContactOffensePlanner.IsInExploreBuildBuffer(snapshot.Owner))
            {
                return false;
            }

            if (!AIMilitaryRallyPlanner.TryResolveRallyAttackTarget(
                    snapshot.Owner,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    ccPosition,
                    out Vector3 attackPoint,
                    out AIMilitaryRallyPlanner.RallyTrigger trigger))
            {
                if (!AIMilitaryRallySessionTracker.ShouldRetainSessionOnLostContact(snapshot.Owner))
                {
                    AIMilitaryRallySessionTracker.Clear(snapshot.Owner);
                }

                return false;
            }

            if (defenseRingMode
                && !AIMilitaryOperationalLeash.IsWithinOperationalZone(
                    attackPoint,
                    ccPosition,
                    operationalRadius))
            {
                return false;
            }

            AIMilitaryRallySessionTracker.RallyPhase rallyPhase = AIMilitaryRallySessionTracker.SyncHostileContact(
                snapshot.Owner,
                attackPoint,
                ccPosition,
                manualOverrides.RallyRegroupStagingRadiusFromCc,
                manualOverrides.RallyRegroupMaxThreatDistanceFraction);

            int minArmy = AIMilitaryRallyPlanner.GetMinimumArmyToRally(
                trigger,
                config.MinArmyBeforeAttack,
                manualOverrides.MinArmyOnHostileContact);
            if (militaryScratch.Count < minArmy)
            {
                return false;
            }

            bool mapExplorationComplete = AIMilitaryMapExplorationUtility.IsExplorationComplete(
                patrolPhase,
                manualOverrides.PatrolMinRadius,
                manualOverrides.PatrolMaxRadius,
                manualOverrides.PatrolRingStep,
                manualOverrides.MapExplorationCompleteCoverage,
                ccPosition,
                fogSystemReference);

            CollectUnifiedRallyArmy(rallyArmyScratch, mapExplorationComplete, minArmy);
            ralliedFullArmy = mapExplorationComplete
                || rallyArmyScratch.Count >= militaryScratch.Count;
            if (rallyArmyScratch.Count < minArmy)
            {
                return false;
            }

            int squadSize = manualOverrides.UnifiedArmySquadSize;
            if (!mapExplorationComplete
                && rallyArmyScratch.Count < squadSize
                && militaryScratch.Count >= squadSize)
            {
                return false;
            }

            MoveCommand move = ResolveMoveCommand(rallyArmyScratch[0]);
            if (move == null)
            {
                return false;
            }

            if (rallyPhase == AIMilitaryRallySessionTracker.RallyPhase.Regrouping)
            {
                Vector3 regroupDestination = attackPoint;
                if (AIMilitaryRallySessionTracker.TryGetRegroupPoint(snapshot.Owner, out Vector3 regroupPoint))
                {
                    regroupDestination = regroupPoint;
                }

                if (AIMilitaryRallySessionTracker.IsRegroupComplete(
                        rallyArmyScratch,
                        regroupDestination,
                        manualOverrides.RallyRegroupGatherRadius,
                        manualOverrides.RallyRegroupRequiredFraction,
                        move))
                {
                    AIMilitaryRallySessionTracker.AdvanceToAttackPhase(snapshot.Owner);
                    rallyPhase = AIMilitaryRallySessionTracker.RallyPhase.Attacking;
                }
                else
                {
                    RaycastHit regroupHit = AIHitUtility.AtPoint(regroupDestination);
                    if (!AIMilitaryArmySquadExecutor.TryFormationAssemble(
                            rallyArmyScratch,
                            regroupDestination,
                            move,
                            24f))
                    {
                        return false;
                    }

                    AIMilitaryRallySessionTracker.MarkRegroupMoveIssued(snapshot.Owner);
                    MarkRallyUnitsAssigned();
                    return true;
                }
            }

            if (rallyPhase != AIMilitaryRallySessionTracker.RallyPhase.Attacking)
            {
                return false;
            }

            if (!AIMilitaryArmySquadExecutor.TryExecuteSquadAttackAndAdvance(
                    snapshot,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    rallyArmyScratch,
                    attackPoint,
                    trigger,
                    move,
                    rallyMoveFallbackScratch,
                    32f))
            {
                return false;
            }

            MarkRallyUnitsAssigned();
            return true;
        }

        private void MarkRallyUnitsAssigned()
        {
            for (int i = 0; i < rallyArmyScratch.Count; i++)
            {
                AbstractUnit unit = rallyArmyScratch[i];
                if (unit != null)
                {
                    assignedMilitaryThisTick.Add(unit.GetInstanceID());
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Rally = một đội thống nhất (10 lính hoặc toàn bộ quân khi map đã mở hết).
        /// Cách hoạt động: CollectArmy — patrol lẻ tách riêng qua <see cref="AIMilitaryLoosePatrolPlanner"/>.
        /// </summary>
        private void CollectUnifiedRallyArmy(
            List<AbstractUnit> output,
            bool mapExplorationComplete,
            int minRequired)
        {
            bool useEntireArmy = mapExplorationComplete
                || militaryScratch.Count <= manualOverrides.UnifiedArmySquadSize;
            int cap = useEntireArmy
                ? militaryScratch.Count
                : manualOverrides.UnifiedArmySquadSize;

            AIMilitaryArmySquadExecutor.CollectArmy(
                militaryScratch,
                assignedMilitaryThisTick,
                cap,
                useEntireArmy,
                output);

            if (output.Count < minRequired && militaryScratch.Count >= minRequired)
            {
                output.Clear();
                AIMilitaryArmySquadExecutor.CollectArmy(
                    militaryScratch,
                    assignedMilitaryThisTick,
                    militaryScratch.Count,
                    useEntireArmy: true,
                    output);
            }
        }

        private void SyncEnemyCivilCentralMemory(AIWorldStateSnapshot snapshot, AIMilitaryRuntimeConfig config)
        {
            if (!AIMilitaryHostileScanner.TryFindEnemyCivilCentral(config.EnemyOwner, out BaseBuilding enemyCc))
            {
                return;
            }

            AIMilitaryEnemyTracker.RememberVisibleEnemyCivilCentral(snapshot.Owner, enemyCc);
        }

        private void EnqueueDefenseIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition)
        {
            int assigned = 0;
            for (int i = 0; i < militaryScratch.Count && assigned < MaxDefenseAssignmentsPerTick; i++)
            {
                AbstractUnit unit = militaryScratch[i];
                AttackCommand attack = ResolveAttackCommand(unit);
                if (attack == null)
                {
                    continue;
                }

                if (!AIMilitaryHostileScanner.TryPickClosestThreat(
                        unit.transform.position,
                        snapshot.Owner,
                        requireVisible: true,
                        threatScratch,
                        out IDamageable unitThreat)
                    || !TryCreateHostileHit(unitThreat, out RaycastHit hit))
                {
                    continue;
                }

                queue.Enqueue(new AICommandIntent(
                    AIMilitaryPriority.AttackThreatNearCc - assigned,
                    AIManagerIds.Military,
                    unit,
                    attack,
                    hit,
                    mouseButton: MouseButton.Right));
                assignedMilitaryThisTick.Add(unit.GetInstanceID());
                assigned++;
            }

        }

        /// <summary>
        /// Mục tiêu: Tuần tra lẻ trong vòng bán kính hiện tại +100m (operational leash), không formation.
        /// </summary>
        private void EnqueueLoosePatrol(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Vector3 ccPosition,
            float operationalRadius,
            bool defenseRingMode)
        {
            if (!manualOverrides.EnableIdlePatrol)
            {
                return;
            }

            patrolPhase++;
            float patrolMin = manualOverrides.PatrolMinRadius;
            float patrolMax = manualOverrides.PatrolMaxRadius;
            if (defenseRingMode && operationalRadius < float.MaxValue * 0.5f)
            {
                patrolMin = Mathf.Min(patrolMin, Mathf.Max(8f, operationalRadius * 0.08f));
                patrolMax = operationalRadius;
            }

            int patrolCap = Mathf.Max(
                manualOverrides.MaxPatrolAssignmentsPerTick,
                Mathf.Min(militaryScratch.Count, 48));

            AIMilitaryLoosePatrolPlanner.EnqueueLoosePatrolIntents(
                snapshot,
                queue,
                militaryScratch,
                assignedMilitaryThisTick,
                patrolPhase,
                ccPosition,
                patrolMin,
                patrolMax,
                operationalRadius,
                patrolCap,
                ResolveMoveCommand);
        }

        private void MarkArmyAssigned(IReadOnlyList<AbstractUnit> army)
        {
            for (int i = 0; i < army.Count; i++)
            {
                AbstractUnit unit = army[i];
                if (unit != null)
                {
                    assignedMilitaryThisTick.Add(unit.GetInstanceID());
                }
            }
        }

        private bool ShouldDeferBarrackTrainingForTower(
            AIWorldStateSnapshot snapshot,
            bool defenseRingMode) =>
            AIMilitaryDefenseRingTowerArmyGate.ShouldDeferBarrackTrainingForTower(
                snapshot,
                manualOverrides,
                defenseRingMode);

        /// <summary>
        /// Mục tiêu: Queue tháp (vòng hoặc tower line) trước train Barrack — priority cao hơn TrainBarrack.
        /// </summary>
        private void EnqueueTowerBuildBeforeBarrackTraining(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            in AIInfluenceMapTickContext influence,
            bool defenseRingMode)
        {
            if (defenseRingMode)
            {
                AIMilitaryDefenseRingBuildCoordinator.EnqueueDefenseRingIntents(
                    snapshot,
                    queue,
                    config,
                    baseSettings,
                    manualOverrides,
                    ccPosition,
                    influence,
                    maxTowerAttemptsPerTick: manualOverrides.MaxTowersPerExpansionPulse);
                return;
            }

            EnqueueTowerLineIfNeeded(snapshot, queue, config, ccPosition, influence);
        }

        private void EnqueueBarrackTrainIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config)
        {
            if (!AIMilitaryExpansionPlanner.ShouldAllowBarrackTraining(snapshot, manualOverrides))
            {
                return;
            }

            if (snapshot.MilitaryUnits.Count >= config.TargetArmyCount)
            {
                return;
            }

            AIMilitaryConfigResolver.CollectOperationalBarracks(
                snapshot,
                manualOverrides,
                barrackScratch,
                trainCommandScratch);
            if (barrackScratch.Count == 0 || trainCommandScratch.Count == 0)
            {
                return;
            }

            AIMilitaryUnitMixPlanner.CountCommittedMilitaryByMixSlot(
                snapshot,
                barrackScratch,
                manualOverrides,
                out int slot0Active,
                out int slot1Active,
                out int slot2Active);

            for (int b = 0; b < barrackScratch.Count; b++)
            {
                BaseBuilding barrack = barrackScratch[b];
                if (barrack.QueueSize >= 5)
                {
                    continue;
                }

                BuildUnitCommand train = AIMilitaryUnitMixPlanner.PickTrainCommand(
                    snapshot,
                    barrack,
                    manualOverrides,
                    ref slot0Active,
                    ref slot1Active,
                    ref slot2Active);
                if (train == null)
                {
                    continue;
                }

                int slotIndex = AIMilitaryUnitMixPlanner.FindMixSlotIndexForCommand(manualOverrides, train);
                if (slotIndex >= 0)
                {
                    AIMilitaryUnitMixPlanner.IncrementPendingTrainSlotCount(
                        slotIndex,
                        ref slot0Active,
                        ref slot1Active,
                        ref slot2Active);
                }

                queue.Enqueue(new AICommandIntent(
                    AIMilitaryPriority.TrainBarrack - b,
                    AIManagerIds.Military,
                    barrack,
                    train,
                    AIHitUtility.AtPoint(barrack.transform.position),
                    mouseButton: MouseButton.Right));
            }
        }

        /// <summary>
        /// Mục tiêu: Bổ sung tháp quanh CC theo hướng có threat (sau tháp đầu do Base xây).
        /// Cách hoạt động: Đếm tháp hoàn thành; chọn slot vòng tròn; worker BuildBuilding trái.
        /// </summary>
        private void EnqueueTowerLineIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            in AIInfluenceMapTickContext influence)
        {
            BuildBuildingCommand towerCommand = config.DefenseTowerBuildCommand;
            if (towerCommand == null
                || AIInfraBuildUtility.CountInfraBuildings(snapshot, AIInfraBuildUtility.BarrackDisplayName) < 1)
            {
                return;
            }

            int towerCount = AIInfraBuildUtility.CountInfraBuildings(
                snapshot,
                AIInfraBuildUtility.DefenseTowerDisplayName);
            int towerCap = ResolveDefenseTowerBuildCap(snapshot);
            if (towerCount >= towerCap
                || AIInfraBuildUtility.HasPendingInfraBuild(
                    snapshot,
                    AIInfraBuildUtility.DefenseTowerDisplayName))
            {
                return;
            }

            if (!SupplyAffordability.HasEnough(snapshot.Owner, towerCommand.Building.Cost))
            {
                return;
            }

            CommandContext lockContext = new(
                snapshot.Owner,
                snapshot.CivilCentral,
                AIHitUtility.AtPoint(ccPosition));
            if (towerCommand.IsLocked(lockContext))
            {
                return;
            }

            if (!TryPickTowerLinePlacement(
                    snapshot,
                    config,
                    ccPosition,
                    towerCount,
                    towerCommand,
                    influence,
                    out Vector3 placement))
            {
                return;
            }

            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(ccPosition, influence);
            PlacementFieldSelectionRegistry.RegisterSelectedPlacement(snapshot.Owner, placement, fieldGrid);

            if (!AIInfraBuildUtility.TryPickBuilderWorker(snapshot, ccPosition, out Worker builder, out bool mustStopGather))
            {
                return;
            }

            AIConstructionAssignment.AssignBuilder(builder.GetInstanceID());
            if (mustStopGather)
            {
                builder.InterruptGatherWorkCycle();
                EnqueueStopBeforeBuild(snapshot, queue, builder);
            }

            queue.Enqueue(new AICommandIntent(
                AIMilitaryPriority.BuildTowerLine,
                AIManagerIds.Military,
                builder,
                towerCommand,
                AIHitUtility.AtPoint(placement),
                mouseButton: MouseButton.Left));

            AIInfraBuildOrderTracker.RegisterOrder(
                snapshot.Owner,
                towerCommand.Building.Name,
                builder.GetInstanceID());
        }

        /// <summary>
        /// Mục tiêu: Hết buffer 2 phút — toàn quân đánh mục tiêu địch gần nhất rồi tiến formation về CC địch.
        /// Cách hoạt động: Resolve entry + CC địch → CollectArmy full → PostContactOffensive.
        /// </summary>
        private bool TryEnqueuePostContactFullOffensive(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition)
        {
            if (snapshot.MilitaryUnits.Count < config.MinArmyBeforeAttack)
            {
                return false;
            }

            if (!AIMilitaryRallyPlanner.TryResolvePostBufferOffensiveTargets(
                    snapshot.Owner,
                    config.EnemyOwner,
                    ccPosition,
                    config.RequireVisibleTargets,
                    out _,
                    out Vector3 enemyCcPoint,
                    out _))
            {
                return false;
            }

            AIMilitaryArmySquadExecutor.CollectArmy(
                militaryScratch,
                assignedMilitaryThisTick,
                militaryScratch.Count,
                useEntireArmy: true,
                unifiedArmyScratch);

            if (unifiedArmyScratch.Count < config.MinArmyBeforeAttack)
            {
                return false;
            }

            MoveCommand move = ResolveMoveCommand(unifiedArmyScratch[0]);
            if (move == null)
            {
                return false;
            }

            if (!AIMilitaryArmySquadExecutor.TryExecutePostContactOffensive(
                    snapshot.Owner,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    ccPosition,
                    enemyCcPoint,
                    unifiedArmyScratch,
                    move,
                    rallyMoveFallbackScratch,
                    40f))
            {
                return false;
            }

            AIMilitaryPostContactOffensePlanner.MarkPostBufferOffensiveLaunched(snapshot.Owner);
            AIMilitaryRallySessionTracker.Clear(snapshot.Owner);
            MarkArmyAssigned(unifiedArmyScratch);
            return true;
        }

        /// <summary>
        /// Mục tiêu: Tấn công tổng — một đội thống nhất (toàn bộ quân): Attack cả nhóm + formation tiến.
        /// </summary>
        private void EnqueueUnifiedArmyOffense(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition)
        {
            int armyCount = snapshot.MilitaryUnits.Count;
            if (armyCount < config.MinArmyBeforeAttack)
            {
                return;
            }

            if (!TryResolveUnifiedAttackPoint(snapshot, config, out Vector3 attackPoint))
            {
                return;
            }

            int enemyArmyNear = AIMilitaryHostileScanner.EstimateEnemyArmyNear(
                attackPoint,
                config.EnemyOwner,
                config.DefenseRadius);
            float requiredArmy = Mathf.Max(config.MinArmyBeforeAttack, enemyArmyNear * config.AttackPowerThreshold);
            if (armyCount < requiredArmy)
            {
                return;
            }

            bool useEntireArmy = manualOverrides.UseFullArmyUnifiedAttack
                || AIMilitaryExpansionPlanner.IsExpansionPhase(snapshot, manualOverrides)
                || armyCount >= manualOverrides.UnifiedArmySquadSize;
            int squadSize = manualOverrides.UnifiedArmySquadSize;
            if (useEntireArmy)
            {
                AIMilitaryArmySquadExecutor.CollectArmy(
                    militaryScratch,
                    assignedMilitaryThisTick,
                    armyCount,
                    useEntireArmy: true,
                    unifiedArmyScratch);
            }
            else if (!AIMilitaryArmySquadExecutor.TryCollectStagingSquad(
                         militaryScratch,
                         assignedMilitaryThisTick,
                         squadSize,
                         unifiedArmyScratch))
            {
                return;
            }

            if (unifiedArmyScratch.Count < manualOverrides.MinUnitsToBeginStagingAssembly)
            {
                return;
            }

            MoveCommand move = ResolveMoveCommand(unifiedArmyScratch[0]);
            if (move == null)
            {
                return;
            }

            bool forceFormationRefresh = AIMilitaryArmyAssemblyTracker.ConsumeFormationRefreshForGrowingArmy(
                snapshot.Owner,
                unifiedArmyScratch.Count);

            AIMilitaryArmyAssemblyTracker.ArmyAssemblyPhase phase =
                AIMilitaryArmyAssemblyTracker.Sync(
                    snapshot.Owner,
                    ccPosition,
                    unifiedArmyScratch,
                    armyCount,
                    manualOverrides,
                    move);

            if (phase == AIMilitaryArmyAssemblyTracker.ArmyAssemblyPhase.Forming
                && AIMilitaryArmyAssemblyTracker.TryGetStagingPoint(
                    snapshot.Owner,
                    ccPosition,
                    manualOverrides,
                    out Vector3 staging))
            {
                staging = AIMilitaryStagingPlacementUtility.AvoidActiveConstructionSites(snapshot, staging);
                if (AIMilitaryArmySquadExecutor.TryFormationAssemble(
                        unifiedArmyScratch,
                        staging,
                        move,
                        manualOverrides.ArmyAssemblyGatherRadius,
                        forceFormationRefresh))
                {
                    MarkArmyAssigned(unifiedArmyScratch);
                }

                return;
            }

            if (!useEntireArmy && unifiedArmyScratch.Count < squadSize)
            {
                return;
            }

            if (useEntireArmy && unifiedArmyScratch.Count < config.MinArmyBeforeAttack)
            {
                return;
            }

            if (AIMilitaryArmySquadExecutor.TryExecuteSquadAttackAndAdvance(
                    snapshot,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    unifiedArmyScratch,
                    attackPoint,
                    AIMilitaryRallyPlanner.RallyTrigger.VisibleEnemyCivilCentral,
                    move,
                    rallyMoveFallbackScratch,
                    36f))
            {
                MarkArmyAssigned(unifiedArmyScratch);
            }
        }

        private bool TryResolveUnifiedAttackPoint(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            out Vector3 attackPoint)
        {
            attackPoint = default;
            if (AIMilitaryHostileScanner.TryFindVisibleEnemyCivilCentral(
                    snapshot.Owner,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    out BaseBuilding enemyCc))
            {
                AIMilitaryEnemyTracker.RememberVisibleEnemyCivilCentral(snapshot.Owner, enemyCc);
                attackPoint = enemyCc.transform.position;
                return true;
            }

            return AIMilitaryEnemyTracker.TryGetLastKnownEnemyCcPosition(
                snapshot.Owner,
                config.EnemyOwner,
                out attackPoint);
        }

        private bool TryPickTowerLinePlacement(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            int existingTowerCount,
            BuildBuildingCommand towerCommand,
            in AIInfluenceMapTickContext influence,
            out Vector3 placement)
        {
            placement = default;
            PlacementFieldGridContext fieldGrid = PlacementFieldSelectionRegistry.ResolveGrid(ccPosition, influence);
            int startSlot = existingTowerCount % TowerLineSlotCount;
            float bestThreatScore = float.MinValue;
            Vector3 bestCandidate = default;
            bool found = false;

            for (int offset = 0; offset < TowerLineSlotCount; offset++)
            {
                int slot = (startSlot + offset) % TowerLineSlotCount;
                float angle = slot * (360f / TowerLineSlotCount) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 anchor = ccPosition + direction * config.TowerLineRadius;

                if (IsTooCloseToExistingTower(snapshot, anchor, config.TowerLineMinSpacing))
                {
                    continue;
                }

                float threatScore = ScoreThreatTowardDirection(threatPositionScratch, ccPosition, direction);
                if (!AIBuildingPlacementUtility.TryFindPlacementExpandingFromAnchor(
                        towerCommand,
                        anchor,
                        config.PlacementSearchRings,
                        config.PlacementSearchStep,
                        expandSearch: offset > 3,
                        snapshot.Owner,
                        fieldGrid,
                        out Vector3 candidate))
                {
                    continue;
                }

                float combined = threatScore;
                if (influence.IsValid
                    && influence.Map.TryGetBestCell(anchor, preferNearAnchorWeight: 0.05f, out Vector3 influencePoint, out float safeScore))
                {
                    if (towerCommand.AllRestrictionsPass(influencePoint)
                        && PlacementFieldSelectionRegistry.CanAcceptPlacement(
                            snapshot.Owner,
                            influencePoint,
                            fieldGrid))
                    {
                        candidate = influencePoint;
                    }

                    combined += safeScore * 0.25f;
                }

                if (!PlacementFieldSelectionRegistry.CanAcceptPlacement(snapshot.Owner, candidate, fieldGrid))
                {
                    continue;
                }

                if (combined <= bestThreatScore)
                {
                    continue;
                }

                bestThreatScore = combined;
                bestCandidate = candidate;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            placement = bestCandidate;
            return true;
        }

        private static float ScoreThreatTowardDirection(
            IReadOnlyList<Vector3> threatPositions,
            Vector3 origin,
            Vector3 direction)
        {
            if (threatPositions.Count == 0)
            {
                return 0f;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return 0f;
            }

            direction.Normalize();
            float score = 0f;
            for (int i = 0; i < threatPositions.Count; i++)
            {
                Vector3 toThreat = threatPositions[i] - origin;
                toThreat.y = 0f;
                float dist = toThreat.magnitude;
                if (dist < 0.01f)
                {
                    continue;
                }

                float alignment = Vector3.Dot(direction, toThreat / dist);
                if (alignment > 0.25f)
                {
                    score += alignment / Mathf.Max(8f, dist);
                }
            }

            return score;
        }

        private static bool IsTooCloseToExistingTower(
            AIWorldStateSnapshot snapshot,
            Vector3 candidate,
            float minSpacing)
        {
            float minSqr = minSpacing * minSpacing;
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO == null
                    || building.BuildingSO.Name != AIInfraBuildUtility.DefenseTowerDisplayName
                    || building.Progress.State == BuildingProgress.BuildingState.Destroyed)
                {
                    continue;
                }

                if ((building.transform.position - candidate).sqrMagnitude < minSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Giới hạn tháp — XP đầu theo EarlyGameTarget; sau đó theo MaxTowersInLine.
        /// </summary>
        private int ResolveDefenseTowerBuildCap(AIWorldStateSnapshot snapshot)
        {
            if (!AIMilitaryExpansionPlanner.HasMetEarlyMilitaryInfraTargets(snapshot, manualOverrides))
            {
                return manualOverrides.EarlyGameTargetDefenseTowerCount;
            }

            return manualOverrides.MaxTowersInLine;
        }

        private static void CollectMilitaryUnits(AIWorldStateSnapshot snapshot, List<AbstractUnit> output)
        {
            output.Clear();
            for (int i = 0; i < snapshot.MilitaryUnits.Count; i++)
            {
                AbstractUnit unit = snapshot.MilitaryUnits[i];
                if (unit != null && unit.CurrentHealth > 0)
                {
                    output.Add(unit);
                }
            }

            output.Sort(static (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        }

        private static void EnqueueStopBeforeBuild(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Worker gatherer)
        {
            StopCommand stop = ResolveStopCommand(gatherer);
            if (stop == null)
            {
                gatherer.Stop();
                return;
            }

            queue.Enqueue(new AICommandIntent(
                AIMilitaryPriority.BuildTowerLine + 5,
                AIManagerIds.Military,
                gatherer,
                stop,
                AIHitUtility.AtPoint(gatherer.transform.position),
                mouseButton: MouseButton.Left));
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

        private static bool TryCreateHostileHit(IDamageable hostile, out RaycastHit hit)
        {
            hit = default;
            if (hostile?.Transform == null)
            {
                return false;
            }

            Collider collider = hostile.Transform.GetComponent<Collider>()
                ?? hostile.Transform.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                hit = AIHitUtility.AtPoint(hostile.Transform.position);
                return true;
            }

            return AIHitUtility.TryCreateHit(collider, out hit);
        }

        private static AttackCommand ResolveAttackCommand(AbstractUnit unit)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is AttackCommand attack)
                {
                    return attack;
                }
            }

            return null;
        }

        private static MoveCommand ResolveMoveCommand(AbstractCommandable commandable)
        {
            if (commandable is not AbstractUnit unit)
            {
                return null;
            }

            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is MoveCommand move)
                {
                    return move;
                }
            }

            return null;
        }
    }
}
