using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units.Visualization;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    [RequireComponent(typeof(NavMeshAgent), typeof(BehaviorGraphAgent), typeof(UnitDeathController))]
    public abstract class AbstractUnit : AbstractCommandable, IMoveable, IAttacker
    {
        public float AgentRadius => Agent.radius;
        [field: SerializeField] public ParticleSystem AttackingParticleSystem { get; private set; }
        [SerializeField] protected DamageableSensor DamageableSensor;
        public NavMeshAgent Agent { get; private set; }
        public Sprite Icon => UnitSO.Icon;
        protected BehaviorGraphAgent graphAgent;
        protected UnitSO unitSO;

        [Header("Movement destination marker")]
        [Tooltip("Spawned at move target when MoveTo is issued; destroyed when the unit finishes this move or changes command.")]
        [SerializeField] private GameObject movementDestinationCursorPrefab;
        [SerializeField] private Color movementDestinationCursorColor = new(0f, 0.85f, 1f, 1f);
        [Tooltip("Minimum time the cursor stays visible so it is not destroyed before NavMesh builds a path.")]
        [SerializeField] private float movementDestinationCursorMinVisibleSeconds = 0.2f;
        [Tooltip("Khoảng cách tới đích (giống MoveToTarget*Action) để xóa marker; tránh remainingDistance=0 trên military.")]
        [SerializeField] private float movementDestinationArrivalSlack = 0.25f;

        [Header("Death")]
        [Tooltip("Ghi đè Death Config trên UnitSO (nếu cần test trên prefab).")]
        [SerializeField] private UnitDeathConfigSO deathConfigOverride;

        private GameObject activeMovementDestinationCursor;
        private Coroutine movementDestinationCursorRoutine;
        private UnitDeathController deathController;
        private bool unitDeathEventRaised;
        private bool unitSpawnEventRaised;
        private bool plannerBlackboardReady;

        protected override void Awake()
        {
            base.Awake();

            Agent = GetComponent<NavMeshAgent>();
            graphAgent = GetComponent<BehaviorGraphAgent>();
            deathController = GetComponent<UnitDeathController>();

            unitSO = UnitSO as UnitSO;

            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
            AttackRangeDisplayInstaller.InstallIfNeeded(gameObject);
        }

        protected override void Start()
        {
            // BehaviorGraphAgent.Init() chạy trong Awake (order -50); bật đọc blackboard trước NotifySpawned.
            plannerBlackboardReady = graphAgent != null && graphAgent.Graph != null;

            base.Start();

            deathController.Configure(unitSO.DeathConfig, deathConfigOverride);

            foreach (UpgradeSO upgrade in unitSO.Upgrades)
            {
                if (unitSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(unitSO);
                }
            }

            MaxHealth = UnitSO.Health;
            CurrentHealth = MaxHealth;

            NotifySpawned();

            if (DamageableSensor != null)
            {
                DamageableSensor.OnUnitEnter += HandleUnitEnter;
                DamageableSensor.OnUnitExit += HandleUnitExit;
                DamageableSensor.Owner = Owner;
                DamageableSensor.SetupFrom(unitSO.AttackConfig);
            }

            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
            SyncMoveSpeedFromUnitSo();
            RefreshVisionFromSightConfig();
        }

        /// <summary>
        /// Mục tiêu: Báo spawn unit một lần (minimap, fog…) sau khi Owner đã gán đúng.
        /// Cách hoạt động: Building queue gọi ngay sau Set Owner; Start gọi lại nếu chưa raise.
        /// </summary>
        public void NotifySpawned()
        {
            if (unitSpawnEventRaised)
            {
                return;
            }

            unitSpawnEventRaised = true;
            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));
        }

        /// <summary>
        /// Gán tốc độ di chuyển từ UnitSO lên NavMeshAgent (sau upgrade / spawn).
        /// </summary>
        private void SyncMoveSpeedFromUnitSo()
        {
            if (unitSO != null)
            {
                Agent.speed = unitSO.MoveSpeed;
            }
        }

        protected override void OnUpgradeAppliedToRuntime()
        {
            if (unitSO == null)
            {
                return;
            }

            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
            SyncMoveSpeedFromUnitSo();

            if (DamageableSensor != null && unitSO.AttackConfig != null)
            {
                DamageableSensor.SetupFrom(unitSO.AttackConfig);
            }
        }

        public virtual void MoveTo(Vector3 position)
        {
            graphAgent.SetVariableValue("TargetLocation", position);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SpawnMovementDestinationCursorAt(position);
        }

        public virtual void MoveTo(Transform transform)
        {
            graphAgent.SetVariableValue("TargetGameObject", transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SpawnMovementDestinationCursorAt(transform.position);
        }

        public virtual void Stop()
        {
            DisposeMovementDestinationCursor();
            SetCommandOverrides(null);
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
        }

        /// <summary>
        /// Mục tiêu: Planner (patrol/scout) chỉ gán lệnh khi unit không đang combat hoặc đang di chuyển tới đích.
        /// Cách hoạt động: Stop, hoặc Move đã tới TargetLocation/TargetGameObject trên NavMesh.
        /// </summary>
        public bool IsAvailableForPlannerPatrol()
        {
            if (!plannerBlackboardReady
                || graphAgent == null
                || CurrentHealth <= 0
                || Agent == null
                || !Agent.isOnNavMesh)
            {
                return false;
            }

            if (!graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable))
            {
                return false;
            }

            return commandVariable.Value switch
            {
                UnitCommands.Stop => true,
                UnitCommands.Move => HasArrivedAtMovementGoal(ResolveMovementGoalFromBlackboard()),
                _ => false
            };
        }

        /// <summary>
        /// Mục tiêu: Planner kiểm tra đích Move hiện tại (tránh spam lệnh tới cùng một điểm).
        /// Cách hoạt động: Chỉ khi Command == Move; đọc TargetGameObject hoặc TargetLocation.
        /// </summary>
        public bool TryGetPlannerMoveGoal(out Vector3 goal)
        {
            goal = transform.position;
            if (graphAgent == null
                || !graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                || commandVariable.Value != UnitCommands.Move)
            {
                return false;
            }

            goal = ResolveMovementGoalFromBlackboard();
            return true;
        }

        /// <summary>
        /// Mục tiêu: Unit đang Move tới vùng gần <paramref name="worldPoint"/> (formation đã gửi).
        /// </summary>
        public bool IsPursuingMoveGoalNear(Vector3 worldPoint, float horizontalTolerance)
        {
            if (!TryGetPlannerMoveGoal(out Vector3 goal))
            {
                return false;
            }

            Vector3 a = goal;
            Vector3 b = worldPoint;
            a.y = 0f;
            b.y = 0f;
            float tolerance = Mathf.Max(2f, horizontalTolerance);
            return (a - b).sqrMagnitude <= tolerance * tolerance;
        }

        public virtual void Attack(IDamageable damageable)
        {
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue("TargetGameObject", damageable.Transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
        }

        public virtual void Attack(Vector3 location)
        {
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", location);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
        }

        protected List<GameObject> UpdateNearbyEnemiesBlackboard()
        {
            List<GameObject> nearbyEnemies = new();

            if (DamageableSensor != null)
            {
                foreach (IDamageable damageable in DamageableSensor.Damageables)
                {
                    if (TryGetEnemyGameObject(damageable, out GameObject enemy))
                    {
                        nearbyEnemies.Add(enemy);
                    }
                }

                nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));
            }

            graphAgent.SetVariableValue("NearbyEnemies", nearbyEnemies);
            return nearbyEnemies;
        }

        /// <summary>
        /// Mục tiêu: Lấy GameObject địch từ sensor mà không NRE khi unit vừa bị Destroy.
        /// Cách hoạt động: Bỏ qua Unity fake-null và Transform null trước khi trả về gameObject.
        /// </summary>
        private static bool TryGetEnemyGameObject(IDamageable damageable, out GameObject enemyGameObject)
        {
            enemyGameObject = null;
            if (damageable is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            Transform enemyTransform = damageable.Transform;
            if (enemyTransform == null)
            {
                return false;
            }

            enemyGameObject = enemyTransform.gameObject;
            return true;
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = UpdateNearbyEnemiesBlackboard();

            if (ShouldAutoAssignNearestEnemyTarget()
                && !HasLockedAttackTarget()
                && !HasLockedMoveTarget()
                && !HasLockedGatherTarget()
                && graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value == null
                && nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }

            OnNearbyEnemyEntered(damageable, nearbyEnemies);
        }

        /// <summary>
        /// Mục tiêu: Cho unit con (WildAnimal) phản ứng ngay khi sensor thấy kẻ địch.
        /// Cách hoạt động: Gọi sau khi cập nhật NearbyEnemies; mặc định không làm gì.
        /// </summary>
        /// <summary>
        /// Mục tiêu: Cho phép unit con (WildAnimal) tắt auto-gán Target khi thấy địch.
        /// Cách hoạt động: WildAnimal trả về false để không bị Move subgraph đuổi theo player.
        /// </summary>
        protected virtual bool ShouldAutoAssignNearestEnemyTarget() => true;

        protected virtual void OnNearbyEnemyEntered(IDamageable damageable, List<GameObject> nearbyEnemies)
        {
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = UpdateNearbyEnemiesBlackboard();

            if (!TryGetEnemyGameObject(damageable, out GameObject exitingEnemy)
                || !graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                || exitingEnemy != targetVariable.Value)
            {
                return;
            }

            // Lệnh Attack có target: giữ TargetGameObject để đuổi/đánh đến chết, không đổi sang enemy gần hơn.
            if (HasLockedAttackTarget())
            {
                return;
            }

            // Move / Gather: không đổi TargetLocation; chỉ bỏ target tạm gán bởi sensor (nếu có).
            if (HasLockedMoveTarget() || HasLockedGatherTarget())
            {
                graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
                return;
            }

            if (nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
            else
            {
                graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
                if (damageable.Transform != null)
                {
                    graphAgent.SetVariableValue("TargetLocation", damageable.Transform.position);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Biết unit đang khóa mục tiêu tấn công (lệnh Attack + TargetGameObject còn sống).
        /// Cách hoạt động: Đọc Command và IDamageable trên TargetGameObject; chỉ sensor/graph bên ngoài bị chặn đổi target.
        /// </summary>
        protected bool HasLockedAttackTarget()
        {
            return TryGetLockedAttackTarget(out _, out IDamageable damageable)
                && damageable.CurrentHealth > 0;
        }

        /// <summary>
        /// Mục tiêu: Lấy target đang khóa cho nhánh Attack.
        /// Cách hoạt động: Command == Attack và TargetGameObject có IDamageable hợp lệ.
        /// </summary>
        protected bool TryGetLockedAttackTarget(out GameObject targetObject, out IDamageable damageable)
        {
            targetObject = null;
            damageable = null;

            if (graphAgent == null
                || !graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                || commandVariable.Value != UnitCommands.Attack
                || !graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                || targetVariable.Value == null
                || !targetVariable.Value.TryGetComponent(out IDamageable targetDamageable))
            {
                return false;
            }

            targetObject = targetVariable.Value;
            damageable = targetDamageable;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Giữ lệnh Move tới TargetLocation; sensor không gán TargetGameObject (animal đi ngang).
        /// Cách hoạt động: Command == Move — graph Move dùng TargetLocation khi TargetGameObject null.
        /// </summary>
        protected bool HasLockedMoveTarget()
        {
            return graphAgent != null
                && graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                && commandVariable.Value == UnitCommands.Move;
        }

        /// <summary>
        /// Mục tiêu: Biết worker đang khóa mỏ supply cho lệnh Gather (đến hết hoặc đổi lệnh mới).
        /// Cách hoạt động: Command == Gather và Supply (hoặc TargetGameObject) còn Amount > 0.
        /// </summary>
        protected bool HasLockedGatherTarget()
        {
            if (graphAgent == null
                || !graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                || commandVariable.Value != UnitCommands.Gather)
            {
                return false;
            }

            if (graphAgent.GetVariable("Supply", out BlackboardVariable<GatherableSupply> supplyVariable)
                && supplyVariable.Value != null)
            {
                return supplyVariable.Value.Amount > 0;
            }

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value != null
                && targetVariable.Value.TryGetComponent(out GatherableSupply supplyFromTarget))
            {
                return supplyFromTarget.Amount > 0;
            }

            return false;
        }

        /// <summary>
        /// Stops tracking and destroys the move-destination cursor (e.g. when issuing Gather/Build or on death).
        /// </summary>
        protected void DisposeMovementDestinationCursor()
        {
            if (movementDestinationCursorRoutine != null)
            {
                StopCoroutine(movementDestinationCursorRoutine);
                movementDestinationCursorRoutine = null;
            }

            if (activeMovementDestinationCursor != null)
            {
                Destroy(activeMovementDestinationCursor);
                activeMovementDestinationCursor = null;
            }
        }

        /// <summary>
        /// Spawns the optional cursor prefab at the move goal and removes it when Nav arrival matches MoveToTarget*Action or command leaves Move.
        /// </summary>
        private void SpawnMovementDestinationCursorAt(Vector3 worldPosition)
        {
            DisposeMovementDestinationCursor();

            if (movementDestinationCursorPrefab == null)
            {
                return;
            }

            activeMovementDestinationCursor = Instantiate(movementDestinationCursorPrefab, worldPosition, Quaternion.identity);
            MovementCursor cursor = activeMovementDestinationCursor.GetComponentInChildren<MovementCursor>();
            if (cursor != null)
            {
                cursor.AnimateOnPos(worldPosition, movementDestinationCursorColor);
            }

            movementDestinationCursorRoutine = StartCoroutine(MovementDestinationCursorLifetimeRoutine());
        }

        private IEnumerator MovementDestinationCursorLifetimeRoutine()
        {
            yield return null;

            float spawnTime = Time.time;
            Vector3 movementGoal = ResolveMovementGoalFromBlackboard();

            while (enabled && graphAgent != null)
            {
                if (!graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> cmdVar)
                    || cmdVar.Value != UnitCommands.Move)
                {
                    break;
                }

                movementGoal = ResolveMovementGoalFromBlackboard();

                bool minTimeElapsed = Time.time - spawnTime >= movementDestinationCursorMinVisibleSeconds;
                if (minTimeElapsed && HasArrivedAtMovementGoal(movementGoal))
                {
                    break;
                }

                yield return null;
            }

            movementDestinationCursorRoutine = null;
            if (activeMovementDestinationCursor != null)
            {
                Destroy(activeMovementDestinationCursor);
                activeMovementDestinationCursor = null;
            }
        }

        /// <summary>
        /// Mục tiêu: lấy điểm đích Move từ blackboard (TargetGameObject hoặc TargetLocation).
        /// Cách hoạt động: ưu tiên vị trí TargetGameObject; không có thì dùng TargetLocation.
        /// </summary>
        private Vector3 ResolveMovementGoalFromBlackboard()
        {
            if (graphAgent != null
                && graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetObject)
                && targetObject.Value != null)
            {
                return targetObject.Value.transform.position;
            }

            if (graphAgent != null
                && graphAgent.GetVariable("TargetLocation", out BlackboardVariable<Vector3> targetLocation))
            {
                return targetLocation.Value;
            }

            return transform.position;
        }

        /// <summary>
        /// Mục tiêu: biết unit đã tới đích Move (khớp logic MoveToTarget* trong Behavior Graph).
        /// Cách hoạt động: path không pending và khoảng cách world tới goal ≤ max(stoppingDistance, slack).
        /// </summary>
        private bool HasArrivedAtMovementGoal(Vector3 goalWorld)
        {
            if (Agent == null || !Agent.isOnNavMesh)
            {
                return false;
            }

            if (Agent.pathPending)
            {
                return false;
            }

            float slack = Mathf.Max(Agent.stoppingDistance, movementDestinationArrivalSlack);
            if (Vector3.Distance(transform.position, goalWorld) <= slack)
            {
                return true;
            }

            return !float.IsInfinity(Agent.remainingDistance) && Agent.remainingDistance <= slack;
        }

        /// <summary>
        /// Gọi từ Animation Event trên clip chết (Function: OnDieAnimationEvent).
        /// </summary>
        public void OnDieAnimationEvent()
        {
            deathController?.NotifyDeathAnimationEvent();
        }

        /// <summary>
        /// Mục tiêu: bắt đầu chết qua Behavior Graph (Command = Die).
        /// Cách hoạt động: raise event, gán Command; graph chạy chuỗi node Death hoặc fallback coroutine.
        /// </summary>
        public override void Die()
        {
            if (IsInDeathSequence)
            {
                return;
            }

            MarkDeathSequenceStarted();
            DisposeMovementDestinationCursor();

            if (IsSelected)
            {
                Deselect();
            }

            unitDeathEventRaised = true;
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));

            graphAgent.SetVariableValue("Command", UnitCommands.Die);
            StartCoroutine(EnsureDeathHandledByBehaviorGraph());
        }

        private IEnumerator EnsureDeathHandledByBehaviorGraph()
        {
            yield return null;
            yield return null;

            if (deathController == null || !IsInDeathSequence || deathController.IsDeathSequenceActive)
            {
                yield break;
            }

            yield return deathController.RunFallbackDeathSequence();

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        protected override void OnDestroy()
        {
            DisposeMovementDestinationCursor();
            base.OnDestroy();

            if (!unitDeathEventRaised)
            {
                Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
            }
        }
    }
}
