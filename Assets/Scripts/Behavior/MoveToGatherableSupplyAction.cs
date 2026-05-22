using GameDevTV.RTS.Environment;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using System.Collections.Generic;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Move to GatherableSupply", story: "[Agent] moves to [Supply] or nearby supply of the same type.", category: "Action/Navigation", id: "b9248f874f11b1a358e671809522dbfc")]
    public partial class MoveToGatherableSupplyAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GatherableSupply> Supply;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(7f);

        private NavMeshAgent agent;
        private Animator animator;
        private LayerMask suppliesMask;
        private SupplySO supplySO;
        private GatherableSupply lockedGatherSupply;

        /// <summary>Kiểm tra agent đã nằm trong vùng chấp nhận quanh điểm đích supply trên bản đồ.</summary>
        /// <remarks>Chỉ dựa remainingDistance dễ Success sớm (0 khi path chưa sẵn hoặc vừa SetDestination).</remarks>
        private bool IsWithinArrivalSlack(Vector3 supplyGoalWorld)
        {
            float slack = Mathf.Max(agent.stoppingDistance, 0.35f);
            return Vector3.Distance(agent.transform.position, supplyGoalWorld) <= slack;
        }

        protected override Status OnStart()
        {
            suppliesMask = LayerMask.GetMask("Supplies");
            lockedGatherSupply = null;

            if (!HasValidInputs())
            {
                return Status.Failure;
            }

            lockedGatherSupply = Supply.Value;
            supplySO = lockedGatherSupply != null ? lockedGatherSupply.Supply : null;
            if (supplySO == null)
            {
                return Status.Failure;
            }

            agent.TryGetComponent(out animator);

            Vector3 targetPosition = GetTargetPosition();

            agent.SetDestination(targetPosition);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Supply.Value == null)
            {
                return Status.Failure;
            }

            if (lockedGatherSupply != null)
            {
                Supply.Value = lockedGatherSupply;
            }

            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, true);
            }

            if (agent.pathPending)
            {
                return Status.Running;
            }

            Vector3 goalWorld = GetTargetPosition();

            if (agent.remainingDistance >= agent.stoppingDistance)
            {
                return Status.Running;
            }

            if (Supply.Value.Amount > 0 && IsWithinArrivalSlack(goalWorld))
            {
                return Status.Success;
            }

            if (lockedGatherSupply != null)
            {
                return Supply.Value.Amount > 0 ? Status.Running : Status.Failure;
            }

            Collider[] colliders = FindNearbySuppliesMatchingType();

            if (colliders.Length > 0)
            {
                Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));

                if (!TryResolveGatherableFromCollider(colliders[0], out GatherableSupply nearby))
                {
                    return Status.Failure;
                }

                Supply.Value = nearby;
                lockedGatherSupply = nearby;
                agent.SetDestination(GetTargetPosition());
                return Status.Running;
            }

            return Status.Failure;
        }

        protected override void OnEnd()
        {
            lockedGatherSupply = null;

            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, false);
            }
        }

        private bool HasValidInputs()
        {
            if (!Agent.Value.TryGetComponent(out agent) || (Supply.Value == null && supplySO == null))
            {
                return false;
            }

            if (Supply.Value != null)
            {
                supplySO = Supply.Value.Supply;
            }
            else
            {
                Collider[] colliders = FindNearbySuppliesMatchingType();
                if (colliders.Length > 0)
                {
                    Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));
                    if (!TryResolveGatherableFromCollider(colliders[0], out GatherableSupply nearby))
                    {
                        return false;
                    }

                    Supply.Value = nearby;
                    supplySO = nearby.Supply;
                }
                else
                {
                    return false;
                }
            }

            return supplySO != null;
        }

        /// <summary>
        /// Tìm mỏ cùng loại còn tài nguyên trong bán kính — dùng supplySO cache, tránh null khi mỏ cạn/hủy.
        /// </summary>
        private Collider[] FindNearbySuppliesMatchingType()
        {
            if (agent == null || supplySO == null)
            {
                return Array.Empty<Collider>();
            }

            float radius = SearchRadius != null ? SearchRadius.Value : 7f;
            Collider[] hits = Physics.OverlapSphere(agent.transform.position, radius, suppliesMask);
            if (hits == null || hits.Length == 0)
            {
                return Array.Empty<Collider>();
            }

            List<Collider> matches = new(hits.Length);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i];
                if (collider == null || !TryResolveGatherableFromCollider(collider, out GatherableSupply supply))
                {
                    continue;
                }

                if (supply.Supply == null || !supply.Supply.Equals(supplySO) || supply.Amount <= 0)
                {
                    continue;
                }

                matches.Add(collider);
            }

            return matches.ToArray();
        }

        private static bool TryResolveGatherableFromCollider(Collider collider, out GatherableSupply supply)
        {
            supply = null;
            if (collider == null)
            {
                return false;
            }

            supply = collider.GetComponent<GatherableSupply>()
                ?? collider.GetComponentInParent<GatherableSupply>();
            return supply != null;
        }

        private Vector3 GetTargetPosition()
        {
            if (Supply.Value == null)
            {
                return agent.transform.position;
            }

            Vector3 targetPosition;
            if (Supply.Value.TryGetComponent(out Collider collider))
            {
                targetPosition = collider.ClosestPoint(agent.transform.position);
            }
            else
            {
                targetPosition = Supply.Value.transform.position;
            }

            return targetPosition;
        }
    }
}
