using GameDevTV.RTS.Units;
using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace GameDevTV.RTS.Behavior.Animal
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Evaluate Animal AI Command",
        story: "[Self] sets Command on blackboard (Attack if fighting, else random Move / Eat / Idle).",
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
            if (Self.Value == null)
            {
                return Status.Failure;
            }

            WildAnimal animal = Self.Value.GetComponent<WildAnimal>()
                ?? Self.Value.GetComponentInParent<WildAnimal>();
            if (animal == null || animal.Config == null)
            {
                Debug.LogWarning(
                    $"{nameof(EvaluateAnimalAICommandAction)} on '{Self.Value.name}': WildAnimal or Animal AI Config is missing.",
                    Self.Value);
                return Status.Failure;
            }

            return animal.EvaluateAndApplyAICommand() ? Status.Success : Status.Failure;
        }
    }
}
