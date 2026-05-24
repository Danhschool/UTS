using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Unit động vật hoang: AI chỉ qua Behavior Graph (blackboard Command + Abort khi Command đổi).
    /// </summary>
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public class WildAnimal : AbstractUnit
    {
        [SerializeField] private AnimalAIConfigSO animalConfig;
        [Tooltip("Faction động vật — phải khác Player1 để unit player tấn công và vào NearbyEnemies.")]
        [SerializeField] private Owner wildlifeOwner = Owner.AI2;

        private bool corpseSupplySpawned;
        private NavMeshAgent cachedAgent;

        public AnimalAIConfigSO Config => animalConfig;

        protected override bool ShouldAutoAssignNearestEnemyTarget() => false;

        protected override void Awake()
        {
            Owner = wildlifeOwner;
            base.Awake();
            cachedAgent = GetComponent<NavMeshAgent>();
            ApplyAttackSensorRangeFromConfig();
        }

        protected override void OnUpgradeAppliedToRuntime()
        {
            base.OnUpgradeAppliedToRuntime();
            ApplyAttackSensorRangeFromConfig();
        }

        protected override void Start()
        {
            base.Start();
            RefreshNearbyEnemies();
            StartCoroutine(BootstrapAnimalAINextFrame());
        }

        public override void TakeDamage(int damage, IDamageable attacker)
        {
            base.TakeDamage(damage, attacker);

            if (IsInDeathSequence || CurrentHealth <= 0)
            {
                return;
            }

            ReactToDamage(attacker);
        }

        public override void Die()
        {
            base.Die();
            StartCoroutine(SpawnCorpseSupplyFallbackAfterDeath());
        }

        public bool TrySpawnCorpseFoodSupply()
        {
            if (corpseSupplySpawned || animalConfig == null || animalConfig.CorpseSupplyPrefab == null)
            {
                return false;
            }

            Vector3 spawnPosition = transform.position;
            Quaternion spawnRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Instantiate(animalConfig.CorpseSupplyPrefab, spawnPosition, spawnRotation);
            corpseSupplySpawned = true;
            return true;
        }

        /// <summary>
        /// Mục tiêu: Gán Command mỗi vòng graph (Evaluate → Switch); Abort Command không bọc cặp này.
        /// Cách hoạt động: Giữ Attack nếu đang đánh; không thì random Move / Eat / Stop.
        /// </summary>
        public bool EvaluateAndApplyAICommand()
        {
            if (animalConfig == null || graphAgent == null)
            {
                return false;
            }

            if (graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                && commandVariable.Value == UnitCommands.Die)
            {
                return true;
            }

            if (HasLockedAttackTarget() || TryMaintainAttackOnCurrentTarget())
            {
                return true;
            }

            ApplyRandomPeacefulCommand();
            return true;
        }

        /// <summary>
        /// Mục tiêu: Đổi Command khi bị đánh; graph Abort (Command changed) chuyển nhánh Attack/Move flee.
        /// Cách hoạt động: Gán Target + Command lên blackboard, không tắt BehaviorGraphAgent.
        /// </summary>
        public void ReactToDamage(IDamageable attacker)
        {
            if (animalConfig == null || graphAgent == null || IsInDeathSequence)
            {
                return;
            }

            RefreshNearbyEnemies();
            GameObject threat = ResolveThreatObject(attacker);
            if (threat == null)
            {
                return;
            }

            ApplyThreatResponse(threat);
        }

        public void RefreshNearbyEnemies()
        {
            if (graphAgent == null)
            {
                return;
            }

            UpdateNearbyEnemiesBlackboard();
        }

        public bool ShouldFlee()
        {
            if (MaxHealth <= 0)
            {
                return false;
            }

            return (float)CurrentHealth / MaxHealth <= animalConfig.FleeHealthFraction;
        }

        public Vector3 ComputeRoamPoint()
        {
            Vector3 origin = transform.position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset2D = Random.insideUnitCircle * animalConfig.RoamRadius;
                Vector3 candidate = origin + new Vector3(offset2D.x, 0f, offset2D.y);
                if (Vector3.Distance(origin, candidate) < animalConfig.RoamMinDistance)
                {
                    continue;
                }

                Vector3 onMesh = SampleOnNavMesh(candidate);
                if (Vector3.Distance(origin, onMesh) >= animalConfig.RoamMinDistance)
                {
                    return onMesh;
                }
            }

            return origin;
        }

        public Vector3 ComputeFleePoint(Vector3 threatPosition)
        {
            Vector3 away = transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Random.insideUnitSphere;
                away.y = 0f;
            }

            away.Normalize();
            Vector3 candidate = transform.position + away * animalConfig.FleeDistance;
            return SampleOnNavMesh(candidate);
        }

        private IEnumerator BootstrapAnimalAINextFrame()
        {
            yield return null;
            EvaluateAndApplyAICommand();
        }

        private bool TryMaintainAttackOnCurrentTarget()
        {
            if (!graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                || commandVariable.Value != UnitCommands.Attack
                || !graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                || targetVariable.Value == null)
            {
                return false;
            }

            if (!targetVariable.Value.TryGetComponent(out IDamageable damageable)
                || !IsHostileDamageable(damageable)
                || damageable.CurrentHealth <= 0)
            {
                return false;
            }

            ApplyAttackCommand(targetVariable.Value);
            return true;
        }

        private bool ApplyThreatResponse(GameObject closestThreat)
        {
            if (closestThreat == null)
            {
                return false;
            }

            if (ShouldFlee())
            {
                ApplyFleeCommand(closestThreat);
                return true;
            }

            ApplyAttackCommand(closestThreat);
            return true;
        }

        private void ApplyAttackCommand(GameObject target)
        {
            EnsureHostileOnNearbyEnemiesList(target);
            graphAgent.SetVariableValue("TargetGameObject", target);
            graphAgent.SetVariableValue("Command", UnitCommands.Attack);
            SetFleeAnimator(false);
        }

        private void ApplyFleeCommand(GameObject threat)
        {
            Vector3 fleePoint = ComputeFleePoint(threat.transform.position);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", fleePoint);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SetFleeAnimator(true);
        }

        private void ApplyEatCommand()
        {
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("Command", UnitCommands.Eat);
            SetFleeAnimator(false);
        }

        private void ApplyRoamCommand()
        {
            Vector3 roamPoint = ComputeRoamPoint();
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("TargetLocation", roamPoint);
            graphAgent.SetVariableValue("Command", UnitCommands.Move);
            SetFleeAnimator(false);
        }

        private void ApplyIdleCommand()
        {
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            SetFleeAnimator(false);
        }

        private void ApplyRandomPeacefulCommand()
        {
            switch (Random.Range(0, 3))
            {
                case 0:
                    ApplyRoamCommand();
                    break;
                case 1:
                    ApplyEatCommand();
                    break;
                default:
                    ApplyIdleCommand();
                    break;
            }
        }

        private void EnsureHostileOnNearbyEnemiesList(GameObject hostile)
        {
            if (hostile == null)
            {
                return;
            }

            List<GameObject> nearby = GetNearbyEnemiesFromBlackboard();
            if (!nearby.Contains(hostile))
            {
                nearby.Add(hostile);
                nearby.Sort(new ClosestGameObjectComparer(transform.position));
            }

            graphAgent.SetVariableValue("NearbyEnemies", nearby);
        }

        private IEnumerator SpawnCorpseSupplyFallbackAfterDeath()
        {
            yield return new WaitForSeconds(2.5f);
            TrySpawnCorpseFoodSupply();
        }

        private GameObject ResolveThreatObject(IDamageable attacker)
        {
            if (IsHostileDamageable(attacker))
            {
                return attacker.Transform.gameObject;
            }

            List<GameObject> threats = GetNearbyEnemiesFromBlackboard();
            if (threats.Count > 0)
            {
                return threats[0];
            }

            return FindNearestHostileViaPhysics();
        }

        private GameObject FindNearestHostileViaPhysics()
        {
            UnitSO animalUnit = UnitSO as UnitSO;
            if (animalUnit?.AttackConfig == null || animalConfig == null)
            {
                return null;
            }

            float radius = animalConfig.AttackSensorRange;
            Collider[] hits = new Collider[16];
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                hits,
                animalUnit.AttackConfig.DamageableLayers);

            GameObject closest = null;
            float closestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].TryGetComponent(out IDamageable damageable) || !IsHostileDamageable(damageable))
                {
                    continue;
                }

                float sqr = (damageable.Transform.position - transform.position).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closestSqr = sqr;
                    closest = damageable.Transform.gameObject;
                }
            }

            return closest;
        }

        private bool IsHostileDamageable(IDamageable damageable)
        {
            return damageable != null
                && damageable.Transform != null
                && damageable.Owner != Owner
                && damageable is Object unityObject
                && unityObject != null;
        }

        private List<GameObject> GetNearbyEnemiesFromBlackboard()
        {
            if (graphAgent.GetVariable("NearbyEnemies", out BlackboardVariable<List<GameObject>> variable)
                && variable.Value != null)
            {
                return variable.Value;
            }

            return new List<GameObject>();
        }

        private Vector3 SampleOnNavMesh(Vector3 worldPosition)
        {
            if (cachedAgent == null)
            {
                return worldPosition;
            }

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, animalConfig.RoamRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return transform.position;
        }

        private void ApplyAttackSensorRangeFromConfig()
        {
            if (animalConfig == null || DamageableSensor == null)
            {
                return;
            }

            SphereCollider sensorCollider = DamageableSensor.GetComponent<SphereCollider>();
            if (sensorCollider != null)
            {
                sensorCollider.radius = animalConfig.AttackSensorRange;
            }
        }

        private void SetFleeAnimator(bool isFleeing)
        {
            if (!TryGetComponent(out Animator animator))
            {
                return;
            }

            animator.SetBool(AnimationConstants.IS_FLEEING, isFleeing);
            if (isFleeing)
            {
                animator.SetBool(AnimationConstants.IS_EATING, false);
            }
        }
    }
}
