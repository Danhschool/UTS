using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
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

        protected override void Awake()
        {
            base.Awake();

            Agent = GetComponent<NavMeshAgent>();
            graphAgent = GetComponent<BehaviorGraphAgent>();
            deathController = GetComponent<UnitDeathController>();

            unitSO = UnitSO as UnitSO;

            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
        }

        protected override void Start()
        {
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

            Bus<UnitSpawnEvent>.Raise(Owner, new UnitSpawnEvent(this));

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
                nearbyEnemies = DamageableSensor.Damageables
                    .ConvertAll(damageable => damageable.Transform.gameObject);
                nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));
            }

            graphAgent.SetVariableValue("NearbyEnemies", nearbyEnemies);
            return nearbyEnemies;
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = UpdateNearbyEnemiesBlackboard();

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value == null && nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = UpdateNearbyEnemiesBlackboard();

            if (!graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                || damageable.Transform.gameObject != targetVariable.Value) return;

            if (nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
            else
            {
                graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
                graphAgent.SetVariableValue("TargetLocation", damageable.Transform.position);
            }
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
