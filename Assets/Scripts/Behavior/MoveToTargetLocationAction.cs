using System;
using GameDevTV.RTS.Units;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Move to Target Location", story: "[Agent] moves to [TargetLocation] .", category: "Action/Navigation", id: "c96373f56a4b683d189e362795d042fa")]
    public partial class MoveToTargetLocationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;
        [Tooltip("Gán BuildingSO (cùng graph đặt nhà): agent đi tới điểm NavMesh gần rìa footprint prefab quanh TargetLocation thay vì tâm.")]
        [SerializeReference] public BlackboardVariable<BuildingSO> PlacementFootprintBuilding;
        [SerializeField] private float footprintNavSampleMaxDistance = 4f;

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 resolvedDestination;

        protected override Status OnStart()
        {
            if (!Agent.Value.TryGetComponent(out agent))
            {
                return Status.Failure;
            }

            Agent.Value.TryGetComponent(out animator);

            resolvedDestination = ResolveDestinationWorld();

            if (Vector3.Distance(agent.transform.position, resolvedDestination) <= agent.stoppingDistance)
            {
                return Status.Success;
            }

            agent.SetDestination(resolvedDestination);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, true);
            }

            if (agent.pathPending)
            {
                return Status.Running;
            }

            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_MOVING, false);
            }
        }

        /// <summary>
        /// Mục tiêu: Chọn điểm đích NavMesh — tâm TargetLocation hoặc mép prefab nhà gần agent nhất.
        /// Cách hoạt động: Nếu có <see cref="PlacementFootprintBuilding"/> thì Instantiate tạm prefab, gom bounds collider/renderer, <c>ClosestPoint</c> từ agent rồi <see cref="NavMesh.SamplePosition"/>.
        /// </summary>
        private Vector3 ResolveDestinationWorld()
        {
            Vector3 center = TargetLocation.Value;
            if (PlacementFootprintBuilding == null
                || PlacementFootprintBuilding.Value == null
                || PlacementFootprintBuilding.Value.Prefab == null)
            {
                return center;
            }

            if (TryGetFootprintEdgeNavPosition(
                    Agent.Value.transform.position,
                    center,
                    PlacementFootprintBuilding.Value.Prefab,
                    footprintNavSampleMaxDistance,
                    out Vector3 navGoal))
            {
                return navGoal;
            }

            return center;
        }

        /// <summary>
        /// Mục tiêu: Tìm điểm trên NavMesh sát rìa bao prefab nhà (gần agent) để worker không dừng ở tâm ô đặt.
        /// Cách hoạt động: Instantiate tạm prefab tại center, Encapsulate bounds collider hoặc renderer, <c>Bounds.ClosestPoint</c> tới agent, SamplePosition.
        /// </summary>
        private static bool TryGetFootprintEdgeNavPosition(
            Vector3 agentWorldPos,
            Vector3 placementCenter,
            GameObject prefab,
            float sampleMaxDistance,
            out Vector3 navPosition)
        {
            navPosition = placementCenter;
            GameObject temp = UnityEngine.Object.Instantiate(prefab, placementCenter, Quaternion.identity);
            try
            {
                foreach (BaseBuilding bb in temp.GetComponentsInChildren<BaseBuilding>(true))
                {
                    bb.enabled = false;
                }

                foreach (NavMeshObstacle obstacle in temp.GetComponentsInChildren<NavMeshObstacle>(true))
                {
                    obstacle.enabled = false;
                }

                bool haveBounds = false;
                Bounds worldBounds = default;

                foreach (Collider col in temp.GetComponentsInChildren<Collider>(true))
                {
                    if (!col.enabled)
                    {
                        continue;
                    }

                    if (!haveBounds)
                    {
                        worldBounds = col.bounds;
                        haveBounds = true;
                    }
                    else
                    {
                        worldBounds.Encapsulate(col.bounds);
                    }
                }

                if (!haveBounds)
                {
                    foreach (Renderer rend in temp.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!haveBounds)
                        {
                            worldBounds = rend.bounds;
                            haveBounds = true;
                        }
                        else
                        {
                            worldBounds.Encapsulate(rend.bounds);
                        }
                    }
                }

                if (!haveBounds)
                {
                    return false;
                }

                Vector3 edgeOrInterior = worldBounds.ClosestPoint(agentWorldPos);
                if (NavMesh.SamplePosition(edgeOrInterior, out NavMeshHit hit, sampleMaxDistance, NavMesh.AllAreas))
                {
                    navPosition = hit.position;
                    return true;
                }

                navPosition = edgeOrInterior;
                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(temp);
            }
        }
    }
}
