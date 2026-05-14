using GameDevTV.RTS.Environment;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;
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

            if (!HasValidInputs())
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

            if (animator != null)
            {
                //animator.SetFloat(AnimationConstants.IS_MOVING, agent.velocity.sqrMagnitude);
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

            Collider[] colliders = FindNearbySuppliesMatchingType();

            if (colliders.Length > 0)
            {
                Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));

                Supply.Value = colliders[0].GetComponent<GatherableSupply>();
                agent.SetDestination(GetTargetPosition());
                return Status.Running;
            }

            return Status.Failure;
        }

        protected override void OnEnd()
        {
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
                    Supply.Value = colliders[0].GetComponent<GatherableSupply>();
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Tìm các supply cùng loại <see cref="SupplySO"/> trong bán kính (nhiều worker có thể cùng khai thác).</summary>
        private Collider[] FindNearbySuppliesMatchingType()
        {
            return Physics.OverlapSphere(
                agent.transform.position,
                SearchRadius,
                suppliesMask
            ).Where(collider =>
                    collider.TryGetComponent(out GatherableSupply supply)
                    && supply.Supply.Equals(Supply.Value.Supply)
            ).ToArray();
        }

        private Vector3 GetTargetPosition()
        {
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
