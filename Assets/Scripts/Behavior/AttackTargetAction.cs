using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using GameDevTV.RTS.Units;
using UnityEngine.AI;
using GameDevTV.RTS.Utilities;
using System.Collections.Generic;
using GameDevTV.RTS.Audio;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Attack Target", story: "[Self] attacks [Target] until it dies.", category: "Action", id: "ec1210263cffd26004732ba1f15cf3c6")]
    public partial class AttackTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AttackConfigSO> AttackConfig;
        [SerializeReference] public BlackboardVariable<List<GameObject>> NearbyEnemies;

        private NavMeshAgent navMeshAgent;
        private AbstractUnit unit;
        private Transform selfTransform;
        private Animator animator;

        private IDamageable targetDamageable;
        private Transform targetTransform;
        private Collider[] enemyColliders;

        private float lastAttackTime;
        private Vector3 approachPoint;

        protected override Status OnStart()
        {
            if (!HasValidInputs()) return Status.Failure;

            selfTransform = Self.Value.transform;
            navMeshAgent = selfTransform.GetComponent<NavMeshAgent>();
            animator = selfTransform.GetComponent<Animator>();
            unit = selfTransform.GetComponent<AbstractUnit>();

            targetTransform = Target.Value.transform;
            targetDamageable = Target.Value.GetComponent<IDamageable>();
            if (AttackConfig.Value.IsAreaOfEffect)
            {
                enemyColliders = new Collider[AttackConfig.Value.MaxEnemiesHitPerAttack];
            }

            approachPoint = GetApproachPoint();

            if (!IsTargetInAttackRange())
            {
                navMeshAgent.SetDestination(approachPoint);
                navMeshAgent.isStopped = false;
                if (animator != null)
                {
                    animator.SetBool(AnimationConstants.IS_ATTACK, false);
                }
            }
            else
            {
                navMeshAgent.isStopped = true;
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null || targetDamageable.CurrentHealth == 0) return Status.Success;

            if (!IsTargetInAttackRange())
            {
                Vector3 newApproach = GetApproachPoint();
                if (Vector3.Distance(newApproach, approachPoint) > 0.25f)
                {
                    approachPoint = newApproach;
                    navMeshAgent.SetDestination(approachPoint);
                }

                if (animator != null)
                {
                    //animator.SetFloat(AnimationConstants.IS_MOVING, navMeshAgent.velocity.magnitude);
                    animator.SetBool(AnimationConstants.IS_MOVING, true);
                }
                return Status.Running;
            }

            navMeshAgent.isStopped = true;
            LookAtTarget();

            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, false);
                animator.SetBool(AnimationConstants.IS_ATTACK, true);

            }

            if (Time.time >= lastAttackTime + AttackConfig.Value.AttackDelay)
            {
                ApplyDamage();
            }

            return Status.Running;
        }

        private void LookAtTarget()
        {
            Vector3 aimPoint = GetApproachPoint();
            Vector3 toTarget = aimPoint - selfTransform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            selfTransform.rotation = Quaternion.Euler(
                selfTransform.rotation.eulerAngles.x,
                lookRotation.eulerAngles.y,
                selfTransform.rotation.eulerAngles.z
            );
        }

        private void ApplyDamage()
        {
            lastAttackTime = Time.time;
            if (unit.AttackingParticleSystem != null)
            {
                unit.AttackingParticleSystem.Play();
            }

            PlayAttackSound();

            if (AttackConfig.Value.HasProjectileAttacks)
            {
                if (selfTransform.TryGetComponent(out IProjectileAttacker projectileAttacker))
                {
                    projectileAttacker.LaunchProjectile(targetDamageable);
                }

                return;
            }

            targetDamageable.TakeDamage(AttackConfig.Value.Damage, unit);

            if (!AttackConfig.Value.IsAreaOfEffect) return;

            int hits = Physics.OverlapSphereNonAlloc(
                targetTransform.position,
                AttackConfig.Value.AreaOfEffectRadius,
                enemyColliders,
                AttackConfig.Value.DamageableLayers
            );

            for (int i = 0; i < hits; i++)
            {
                if (enemyColliders[i].TryGetComponent(out IDamageable nearbyDamageable)
                    && targetDamageable != nearbyDamageable)
                {
                    nearbyDamageable.TakeDamage(
                        AttackConfig.Value.CalculateAreaOfEffectDamage(
                            targetTransform.position,
                            nearbyDamageable.Transform.position),
                        unit);
                }
            }
        }

        void PlayAttackSound()
        {
            if (AttackConfig.Value.HasProjectileAttacks)
            {
                return;
            }

            AudioCueId cue = AttackAudioUtility.ResolveAttackCue(AttackConfig.Value);
            if (cue == AudioCueId.None || selfTransform == null)
            {
                return;
            }

            AudioAccess.TryPlay3D(cue, selfTransform.position);
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_ATTACK, false);
            }
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
            }
        }

        private bool HasValidInputs() => Self.Value != null && Self.Value.TryGetComponent(out NavMeshAgent _)
            && Self.Value.TryGetComponent(out AbstractUnit _)
            && Target.Value != null && Target.Value.TryGetComponent(out IDamageable _)
            && AttackConfig.Value != null;

        private Vector3 GetApproachPoint() =>
            Target.Value != null
                ? CombatTargetGeometryUtility.GetClosestPointOnTarget(selfTransform.position, Target.Value)
                : targetTransform != null
                    ? targetTransform.position
                    : selfTransform.position;

        /// <summary>
        /// Mục tiêu: Cho phép tấn công khi đủ tầm tới rìa mục tiêu (collider / NavMeshObstacle), không phải tâm pivot.
        /// Cách hoạt động: So khoảng cách 3D tới điểm gần nhất trên footprint với AttackRange (+ slack).
        /// </summary>
        private bool IsTargetInAttackRange()
        {
            if (Target.Value == null || AttackConfig.Value == null)
            {
                return false;
            }

            float slack = Mathf.Max(0.15f, navMeshAgent != null ? navMeshAgent.stoppingDistance : 0.15f);
            float range = AttackConfig.Value.AttackRange + slack;
            float distance = CombatTargetGeometryUtility.GetDistanceToTargetSurface(
                selfTransform.position,
                Target.Value);
            return distance <= range;
        }
    }

}
