using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Move to Target GameObject", story: "[Agent] moves to [TargetGameObject] .", category: "Action/Navigation", id: "f07a8fab1fc459315f3380eef35b2aa0")]
    public partial class MoveToTargetGameObjectAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;
        [SerializeReference] public BlackboardVariable<float> MoveThreshold = new(0.25f);

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 lockedDestination;
        private bool hasLockedDestination;

        /// <summary>Kiểm tra agent đã tới gần điểm đích trong không gian thế giới (tránh Success sớm chỉ dựa vào remainingDistance).</summary>
        /// <remarks>Dùng max(stoppingDistance, MoveThreshold) làm ngưỡng slack; phù hợp khi stoppingDistance NavMesh rất nhỏ hoặc remainingDistance báo sai lúc path pending.</remarks>
        private bool IsWithinArrivalSlack(Vector3 goalWorld)
        {
            float slack = Mathf.Max(agent.stoppingDistance, MoveThreshold != null ? MoveThreshold.Value : 0.25f);
            return Vector3.Distance(agent.transform.position, goalWorld) <= slack;
        }

        /// <summary>Coi là đã tới khi path không còn pending và vị trí thực nằm trong slack tới goal.</summary>
        /// <remarks>Không chỉ dựa remainingDistance vì giá trị này có thể 0/sai trước khi path ổn định, gây Success sớm.</remarks>
        private bool HasArrivedAt(Vector3 goalWorld)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return false;
            }

            if (agent.pathPending)
            {
                return false;
            }

            return IsWithinArrivalSlack(goalWorld);
        }

        /// <summary>
        /// Mục tiêu: Khóa một điểm đích duy nhất lúc bắt đầu node — tránh ClosestPoint trượt theo rìa target mỗi frame.
        /// Cách hoạt động: GameObject → ClosestPoint một lần; không có GO → TargetLocation trên blackboard.
        /// </summary>
        private bool TryLockDestinationAtStart()
        {
            hasLockedDestination = false;

            if (TargetGameObject.Value != null)
            {
                lockedDestination = CombatTargetGeometryUtility.GetClosestPointOnTarget(
                    agent.transform.position,
                    TargetGameObject.Value);
                hasLockedDestination = true;
                return true;
            }

            if (Agent.Value.TryGetComponent(out BehaviorGraphAgent graphAgent)
                && graphAgent.GetVariable("TargetLocation", out BlackboardVariable<Vector3> targetLocation))
            {
                lockedDestination = targetLocation.Value;
                hasLockedDestination = true;
                return true;
            }

            return false;
        }

        protected override Status OnStart()
        {
            hasLockedDestination = false;

            if (!Agent.Value.TryGetComponent(out agent))
            {
                return Status.Failure;
            }

            Agent.Value.TryGetComponent(out animator);

            if (!TryLockDestinationAtStart())
            {
                return Status.Failure;
            }

            if (IsWithinArrivalSlack(lockedDestination))
            {
                return Status.Success;
            }

            agent.SetDestination(lockedDestination);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (!hasLockedDestination)
            {
                return Status.Failure;
            }

            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, true);
            }

            if (HasArrivedAt(lockedDestination))
            {
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            hasLockedDestination = false;

            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, false);
            }
        }
    }
}
