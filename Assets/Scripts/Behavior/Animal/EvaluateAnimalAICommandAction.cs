using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

namespace GameDevTV.RTS.Behavior.Animal
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Evaluate Animal AI Command",
        story: "[Self] picks Command (Move / Attack / Eat / Die) from threats and config.",
        category: "Action/Animal",
        id: "a1b2c3d4e5f60718293a4b5c6d7e8f01")]
    public partial class EvaluateAnimalAICommandAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<UnitCommands> Command;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;
        [SerializeReference] public BlackboardVariable<List<GameObject>> NearbyEnemies;

        protected override Status OnStart()
        {
            if (Self.Value == null
                || !Self.Value.TryGetComponent(out WildAnimal animal)
                || animal.Config == null)
            {
                return Status.Failure;
            }

            if (Command.Value == UnitCommands.Die)
            {
                return Status.Success;
            }

            animal.RefreshNearbyEnemies();
            List<GameObject> threats = NearbyEnemies.Value ?? new List<GameObject>();

            if (threats.Count > 0)
            {
                GameObject closestThreat = threats[0];
                TargetGameObject.Value = closestThreat;

                if (animal.ShouldFlee())
                {
                    Command.Value = UnitCommands.Move;
                    TargetLocation.Value = ComputeFleePoint(animal, closestThreat.transform.position);
                    SetFleeAnimator(animal, true);
                    return Status.Success;
                }

                Command.Value = UnitCommands.Attack;
                SetFleeAnimator(animal, false);
                return Status.Success;
            }

            SetFleeAnimator(animal, false);

            if (animal.RollEatThisTick())
            {
                Command.Value = UnitCommands.Eat;
                TargetGameObject.Value = null;
                return Status.Success;
            }

            Command.Value = UnitCommands.Move;
            TargetGameObject.Value = null;
            TargetLocation.Value = ComputeRoamPoint(animal);
            return Status.Success;
        }

        private static Vector3 ComputeFleePoint(WildAnimal animal, Vector3 threatPosition)
        {
            Vector3 away = animal.transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = UnityEngine.Random.insideUnitSphere;
                away.y = 0f;
            }

            away.Normalize();
            Vector3 candidate = animal.transform.position + away * animal.Config.FleeDistance;
            return SampleOnNavMesh(animal, candidate);
        }

        private static Vector3 ComputeRoamPoint(WildAnimal animal)
        {
            Vector3 origin = animal.transform.position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset2D = UnityEngine.Random.insideUnitCircle * animal.Config.RoamRadius;
                Vector3 candidate = origin + new Vector3(offset2D.x, 0f, offset2D.y);
                if (Vector3.Distance(origin, candidate) < animal.Config.RoamMinDistance)
                {
                    continue;
                }

                Vector3 onMesh = SampleOnNavMesh(animal, candidate);
                if (Vector3.Distance(origin, onMesh) >= animal.Config.RoamMinDistance)
                {
                    return onMesh;
                }
            }

            return origin;
        }

        private static Vector3 SampleOnNavMesh(WildAnimal animal, Vector3 worldPosition)
        {
            if (!animal.TryGetComponent(out NavMeshAgent agent))
            {
                return worldPosition;
            }

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, animal.Config.RoamRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return animal.transform.position;
        }

        private static void SetFleeAnimator(WildAnimal animal, bool isFleeing)
        {
            if (!animal.TryGetComponent(out Animator animator))
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
