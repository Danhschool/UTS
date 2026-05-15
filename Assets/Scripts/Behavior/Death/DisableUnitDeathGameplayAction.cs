using System;
using GameDevTV.RTS.Units;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace GameDevTV.RTS.Behavior.Death
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Disable Unit Death Gameplay",
        story: "[Self] disables agent, sensors, colliders, and non-visual children for death.",
        category: "Action/Units/Death",
        id: "c4e8a1f203b5476d9e0a1b2c3d4e5f60")]
    public partial class DisableUnitDeathGameplayAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out UnitDeathController controller))
            {
                return Status.Failure;
            }

            controller.PrepareDeath();
            return Status.Success;
        }
    }
}
