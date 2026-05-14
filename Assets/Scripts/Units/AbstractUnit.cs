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
    [RequireComponent(typeof(NavMeshAgent), typeof(BehaviorGraphAgent))]
    public abstract class AbstractUnit : AbstractCommandable, IMoveable, IAttacker
    {
        public float AgentRadius => Agent.radius;
        [field: SerializeField] public ParticleSystem AttackingParticleSystem { get; private set; }
        [SerializeField] private DamageableSensor DamageableSensor;
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

        private GameObject activeMovementDestinationCursor;
        private Coroutine movementDestinationCursorRoutine;

        protected override void Awake()
        {
            base.Awake();

            Agent = GetComponent<NavMeshAgent>();
            graphAgent = GetComponent<BehaviorGraphAgent>();

            unitSO = UnitSO as UnitSO;

            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            graphAgent.SetVariableValue("AttackConfig", unitSO.AttackConfig);
        }

        protected override void Start()
        {
            base.Start();

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

        public void MoveTo(Vector3 position)
        {
            graphAgent.SetVariableValue("TargetLocation", position);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SpawnMovementDestinationCursorAt(position);
        }

        public void MoveTo(Transform transform)
        {
            graphAgent.SetVariableValue("TargetGameObject", transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SpawnMovementDestinationCursorAt(transform.position);
        }

        public void Stop()
        {
            DisposeMovementDestinationCursor();
            SetCommandOverrides(null);
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
        }

        public void Attack(IDamageable damageable)
        {
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue("TargetGameObject", damageable.Transform.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
        }

        public void Attack(Vector3 location)
        {
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", location);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
        }

        private void HandleUnitEnter(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value == null && nearbyEnemies.Count > 0)
            {
                graphAgent.SetVariableValue("TargetGameObject", nearbyEnemies[0]);
            }
        }

        private void HandleUnitExit(IDamageable damageable)
        {
            List<GameObject> nearbyEnemies = SetNearbyEnemiesOnBlackboard();

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

        private List<GameObject> SetNearbyEnemiesOnBlackboard()
        {
            List<GameObject> nearbyEnemies = DamageableSensor.Damageables
                            .ConvertAll(damageable => damageable.Transform.gameObject);
            nearbyEnemies.Sort(new ClosestGameObjectComparer(transform.position));

            graphAgent.SetVariableValue("NearbyEnemies", nearbyEnemies);

            return nearbyEnemies;
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

            while (enabled && graphAgent != null)
            {
                if (!graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> cmdVar)
                    || cmdVar.Value != UnitCommands.Move)
                {
                    break;
                }

                bool minTimeElapsed = Time.time - spawnTime >= movementDestinationCursorMinVisibleSeconds;
                bool pathReady = Agent != null && Agent.hasPath && !Agent.pathPending;
                bool atDestination = Agent != null && Agent.remainingDistance <= Agent.stoppingDistance;

                if (minTimeElapsed && pathReady && atDestination)
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

        protected override void OnDestroy()
        {
            DisposeMovementDestinationCursor();
            base.OnDestroy();
            Bus<UnitDeathEvent>.Raise(Owner, new UnitDeathEvent(this));
        }
    }
}
