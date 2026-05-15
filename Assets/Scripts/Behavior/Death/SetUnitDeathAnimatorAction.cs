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
        name: "Set Unit Death Animator",
        story: "[Self] sets isDying and clears movement/attack animator bools.",
        category: "Action/Units/Death",
        id: "d5f9b2a314c6587e0f1b2c3d4e5f6a71")]
    public partial class SetUnitDeathAnimatorAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out UnitDeathController controller))
            {
                return Status.Failure;
            }

            controller.SetDeathAnimator();
            return Status.Success;
        }
    }
}
