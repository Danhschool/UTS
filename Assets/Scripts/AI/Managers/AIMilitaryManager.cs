using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
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
        private const int MaxDefenseAssignmentsPerTick = 16;
        private const int MaxAttackWaveAssignmentsPerTick = 24;
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
        private readonly AIMilitarySquadTracker squadTracker = new();
        private readonly List<AbstractUnit> patrolCandidatesScratch = new(32);
        private readonly List<List<AbstractUnit>> patrolSquadsScratch = new(8);
        private readonly List<AbstractUnit> patrolSolosScratch = new(32);
        private readonly List<AbstractUnit> rallyArmyScratch = new(48);
        private readonly List<AbstractUnit> attackWaveMoveScratch = new(48);
        private readonly List<AbstractUnit> rallyMoveFallbackScratch = new(48);
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

            bool armyRalliedOnContact = false;
            bool ralliedFullArmy = true;
            if (threatCount > 0)
            {
                EnqueueDefenseIntents(snapshot, queue, config, ccPosition);
            }
            else if (TryEnqueueArmyRallyOnHostileContact(snapshot, config, ccPosition, out ralliedFullArmy))
            {
                armyRalliedOnContact = true;
            }
            else
            {
                EnqueueBarrackTrainIfNeeded(snapshot, queue, config);
                EnqueueTowerLineIfNeeded(snapshot, queue, config, ccPosition, influence);
                if (config.EnableAttack)
                {
                    EnqueueAttackWaveIfReady(snapshot, queue, config);
                }
            }

            if (!armyRalliedOnContact || !ralliedFullArmy)
            {
                EnqueueIdlePatrolIntents(snapshot, queue, ccPosition);
            }
        }

        /// <summary>
        /// Mục tiêu: Thấy địch — tập hợp xa gần nhà → phase Attacking = lệnh Attack (ưu tiên lính địch).
        /// Cách hoạt động: Regroup gần CC ta → đủ quân hoặc 1 lệnh lùi → AttackCommand; Move chỉ fallback.
        /// </summary>
        private bool TryEnqueueArmyRallyOnHostileContact(
            AIWorldStateSnapshot snapshot,
            AIMilitaryRuntimeConfig config,
            Vector3 ccPosition,
            out bool ralliedFullArmy)
        {
            ralliedFullArmy = true;
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
                manualOverrides.MapExplorationCompleteCoverage,
                ccPosition,
                fogSystemReference);

            CollectRallyArmy(rallyArmyScratch, mapExplorationComplete, minArmy);
            ralliedFullArmy = mapExplorationComplete
                || rallyArmyScratch.Count >= militaryScratch.Count;
            if (rallyArmyScratch.Count < minArmy)
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
                        manualOverrides.RallyRegroupRequiredFraction))
                {
                    AIMilitaryRallySessionTracker.AdvanceToAttackPhase(snapshot.Owner);
                    rallyPhase = AIMilitaryRallySessionTracker.RallyPhase.Attacking;
                }
                else
                {
                    RaycastHit regroupHit = AIHitUtility.AtPoint(regroupDestination);
                    if (!GroupFormationMoveUtility.TryApplyMoveIfNeeded(rallyArmyScratch, regroupHit, move, 24f))
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

            if (!AIMilitaryRallyCombatPlanner.TryExecuteAttackPhase(
                    rallyArmyScratch,
                    snapshot.Owner,
                    config.EnemyOwner,
                    config.RequireVisibleTargets,
                    attackPoint,
                    trigger,
                    move,
                    rallyMoveFallbackScratch))
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
        /// Mục tiêu: Chọn lính đi rally — 100% nếu map đã explore hết, không thì ~80% (phần sau list giữ scout).
        /// Cách hoạt động: Giữ floor(N × (1 − fraction)) lính cuối danh sách không rally.
        /// </summary>
        private void CollectRallyArmy(List<AbstractUnit> output, bool mapExplorationComplete, int minRequired)
        {
            output.Clear();
            int total = militaryScratch.Count;
            int rallyCount = total;

            if (!mapExplorationComplete && total > 0)
            {
                float fraction = Mathf.Clamp01(manualOverrides.RallyFractionWhileExploring);
                int keepExploring = Mathf.FloorToInt(total * (1f - fraction));
                rallyCount = total - keepExploring;
                rallyCount = Mathf.Clamp(rallyCount, minRequired, total);
            }

            for (int i = 0; i < rallyCount; i++)
            {
                AbstractUnit unit = militaryScratch[i];
                if (unit != null && unit.CurrentHealth > 0)
                {
                    output.Add(unit);
                }
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
        /// Mục tiêu: Quân rảnh patrol ngẫu nhiên; nhóm gần đủ lâu → formation move như multi-select.
        /// Cách hoạt động: Gom squad → GroupFormationMoveUtility; lẻ → enqueue Move (giới hạn/tick).
        /// </summary>
        private void EnqueueIdlePatrolIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Vector3 ccPosition)
        {
            if (!manualOverrides.EnableIdlePatrol)
            {
                return;
            }

            patrolPhase++;
            int cap = Mathf.Max(1, manualOverrides.MaxPatrolAssignmentsPerTick);
            CollectPatrolCandidates(cap);

            squadTracker.PartitionIdleUnits(
                patrolCandidatesScratch,
                manualOverrides.SquadMergeRadius,
                manualOverrides.SquadMergeHoldSeconds,
                manualOverrides.MinSquadMembers,
                patrolSquadsScratch,
                patrolSolosScratch);

            int assigned = 0;
            assigned += EnqueueSquadPatrolMoves(snapshot, ccPosition);
            assigned += EnqueueSoloPatrolIntents(snapshot, queue, ccPosition, cap - assigned);
        }

        private void CollectPatrolCandidates(int cap)
        {
            patrolCandidatesScratch.Clear();
            for (int i = 0; i < militaryScratch.Count && patrolCandidatesScratch.Count < cap; i++)
            {
                AbstractUnit unit = militaryScratch[i];
                if (assignedMilitaryThisTick.Contains(unit.GetInstanceID())
                    || !AIMilitaryPatrolUtility.IsEligibleForPatrol(unit))
                {
                    continue;
                }

                patrolCandidatesScratch.Add(unit);
            }
        }

        private int EnqueueSquadPatrolMoves(AIWorldStateSnapshot snapshot, Vector3 ccPosition)
        {
            int assigned = 0;
            for (int s = 0; s < patrolSquadsScratch.Count; s++)
            {
                List<AbstractUnit> squad = patrolSquadsScratch[s];
                if (squad == null || squad.Count < manualOverrides.MinSquadMembers)
                {
                    continue;
                }

                if (!AIMilitaryPatrolUtility.TryGetRandomPatrolDestination(
                        ccPosition,
                        patrolPhase,
                        manualOverrides.PatrolMinRadius,
                        manualOverrides.PatrolMaxRadius,
                        out Vector3 patrolPoint))
                {
                    continue;
                }

                MoveCommand move = ResolveMoveCommand(squad[0]);
                if (move == null)
                {
                    continue;
                }

                RaycastHit hit = AIHitUtility.AtPoint(patrolPoint);
                if (!GroupFormationMoveUtility.TryApplyMoveIfNeeded(squad, hit, move, 20f))
                {
                    continue;
                }

                for (int i = 0; i < squad.Count; i++)
                {
                    if (squad[i] != null)
                    {
                        assignedMilitaryThisTick.Add(squad[i].GetInstanceID());
                        assigned++;
                    }
                }
            }

            return assigned;
        }

        private int EnqueueSoloPatrolIntents(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            Vector3 ccPosition,
            int remainingCap)
        {
            if (remainingCap <= 0)
            {
                return 0;
            }

            int assigned = 0;
            for (int i = 0; i < patrolSolosScratch.Count && assigned < remainingCap; i++)
            {
                AbstractUnit unit = patrolSolosScratch[i];
                if (unit == null || assignedMilitaryThisTick.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (!AIMilitaryPatrolUtility.TryGetRandomPatrolDestination(
                        ccPosition,
                        patrolPhase,
                        manualOverrides.PatrolMinRadius,
                        manualOverrides.PatrolMaxRadius,
                        out Vector3 patrolPoint))
                {
                    continue;
                }

                if (!TryEnqueuePatrolMove(snapshot, queue, unit, patrolPoint, assigned))
                {
                    continue;
                }

                assignedMilitaryThisTick.Add(unit.GetInstanceID());
                assigned++;
            }

            return assigned;
        }

        private bool TryEnqueuePatrolMove(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AbstractUnit unit,
            Vector3 patrolPoint,
            int orderIndex)
        {
            MoveCommand move = ResolveMoveCommand(unit);
            if (move == null)
            {
                return false;
            }

            RaycastHit hit = AIHitUtility.AtPoint(patrolPoint);
            CommandContext probe = new(snapshot.Owner, unit, hit, orderIndex, MouseButton.Right);
            if (!move.CanHandle(probe))
            {
                return false;
            }

            queue.Enqueue(new AICommandIntent(
                AIMilitaryPriority.PatrolExpandMap - orderIndex,
                AIManagerIds.Military,
                unit,
                move,
                hit,
                orderIndex,
                MouseButton.Right));
            return true;
        }

        private void EnqueueBarrackTrainIfNeeded(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config)
        {
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
                || !AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.BarrackDisplayName))
            {
                return;
            }

            int towerCount = CountCompletedDefenseTowers(snapshot);
            if (towerCount >= config.MaxTowersInLine
                || AIInfraBuildOrderTracker.HasOrderedBuild(snapshot.Owner, AIInfraBuildUtility.DefenseTowerDisplayName))
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
        /// Mục tiêu: Gửi wave tấn công Civil Central địch khi đủ quân và biết vị trí mục tiêu.
        /// Cách hoạt động: So army với ngưỡng; Attack CC nếu visible; Move formation tới memory.
        /// </summary>
        private void EnqueueAttackWaveIfReady(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AIMilitaryRuntimeConfig config)
        {
            int armyCount = snapshot.MilitaryUnits.Count;
            if (armyCount < config.MinArmyBeforeAttack || armyCount < config.TargetArmyCount)
            {
                return;
            }

            bool hasVisibleEnemyCc = AIMilitaryHostileScanner.TryFindEnemyCivilCentral(
                config.EnemyOwner,
                out BaseBuilding visibleEnemyCc);
            if (hasVisibleEnemyCc)
            {
                AIMilitaryEnemyTracker.RememberVisibleEnemyCivilCentral(snapshot.Owner, visibleEnemyCc);
            }

            Vector3 attackPoint;
            if (hasVisibleEnemyCc)
            {
                attackPoint = visibleEnemyCc.transform.position;
            }
            else if (!AIMilitaryEnemyTracker.TryGetLastKnownEnemyCcPosition(
                         snapshot.Owner,
                         config.EnemyOwner,
                         out attackPoint))
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

            attackWaveMoveScratch.Clear();
            int attackOrders = 0;
            for (int i = 0; i < militaryScratch.Count && attackOrders < MaxAttackWaveAssignmentsPerTick; i++)
            {
                AbstractUnit unit = militaryScratch[i];
                if (assignedMilitaryThisTick.Contains(unit.GetInstanceID()))
                {
                    continue;
                }

                if (hasVisibleEnemyCc
                    && visibleEnemyCc.IsVisible
                    && TryEnqueueAttackOnEnemyCivilCentral(snapshot, queue, unit, visibleEnemyCc, attackOrders))
                {
                    assignedMilitaryThisTick.Add(unit.GetInstanceID());
                    attackOrders++;
                    continue;
                }

                attackWaveMoveScratch.Add(unit);
            }

            if (attackWaveMoveScratch.Count > 0)
            {
                TryApplyAttackWaveFormationMove(attackWaveMoveScratch, attackPoint);
            }
        }

        /// <summary>
        /// Mục tiêu: Wave tiến gần CC địch theo formation — không gửi mọi unit tới cùng một điểm.
        /// </summary>
        private void TryApplyAttackWaveFormationMove(List<AbstractUnit> units, Vector3 attackPoint)
        {
            MoveCommand move = ResolveMoveCommand(units[0]);
            if (move == null)
            {
                return;
            }

            RaycastHit hit = AIHitUtility.AtPoint(attackPoint);
            if (!GroupFormationMoveUtility.TryApplyMoveIfNeeded(units, hit, move, 26f))
            {
                return;
            }

            for (int i = 0; i < units.Count; i++)
            {
                AbstractUnit unit = units[i];
                if (unit != null)
                {
                    assignedMilitaryThisTick.Add(unit.GetInstanceID());
                }
            }
        }

        private bool TryEnqueueAttackOnEnemyCivilCentral(
            AIWorldStateSnapshot snapshot,
            AIPriorityQueue queue,
            AbstractUnit unit,
            BaseBuilding enemyCc,
            int orderIndex)
        {
            AttackCommand attack = ResolveAttackCommand(unit);
            if (attack == null)
            {
                return false;
            }

            Collider collider = enemyCc.GetComponent<Collider>()
                ?? enemyCc.GetComponentInChildren<Collider>();
            if (collider == null || !AIHitUtility.TryCreateHit(collider, out RaycastHit hit))
            {
                return false;
            }

            CommandContext probe = new(snapshot.Owner, unit, hit, mouseButton: MouseButton.Right);
            if (!attack.CanHandle(probe))
            {
                return false;
            }

            queue.Enqueue(new AICommandIntent(
                AIMilitaryPriority.AttackEnemyCivilCentral - orderIndex,
                AIManagerIds.Military,
                unit,
                attack,
                hit,
                mouseButton: MouseButton.Right));
            return true;
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
                        out Vector3 candidate))
                {
                    continue;
                }

                float combined = threatScore;
                if (influence.IsValid
                    && influence.Map.TryGetBestCell(anchor, preferNearAnchorWeight: 0.05f, out Vector3 influencePoint, out float safeScore))
                {
                    if (towerCommand.AllRestrictionsPass(influencePoint))
                    {
                        candidate = influencePoint;
                    }

                    combined += safeScore * 0.25f;
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

        private static int CountCompletedDefenseTowers(AIWorldStateSnapshot snapshot)
        {
            int count = 0;
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.BuildingSO == null
                    || building.BuildingSO.Name != AIInfraBuildUtility.DefenseTowerDisplayName
                    || building.Progress.State != BuildingProgress.BuildingState.Completed)
                {
                    continue;
                }

                count++;
            }

            return count;
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
