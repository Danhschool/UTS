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
        name: "Freeze Unit Death Pose",
        story: "[Self] freezes animator on the last frame of the death clip.",
        category: "Action/Units/Death",
        id: "f7b1d4c536e87a901b2c3d4e5f6a8c93")]
    public partial class FreezeUnitDeathPoseAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out UnitDeathController controller))
            {
                return Status.Failure;
            }

            controller.FreezeDeathPose();
            return Status.Success;
        }
    }
}
