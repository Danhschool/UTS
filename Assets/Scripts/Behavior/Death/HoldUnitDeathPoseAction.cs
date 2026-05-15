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
        name: "Hold Unit Death Pose",
        story: "[Self] holds the frozen death pose for the configured duration.",
        category: "Action/Units/Death",
        id: "a8c2e5d647f98b012c3d4e5f6a9d0e4")]
    public partial class HoldUnitDeathPoseAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        private UnitDeathController deathController;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out deathController))
            {
                return Status.Failure;
            }

            deathController.BeginHoldDeathPose();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (deathController == null)
            {
                return Status.Success;
            }

            return deathController.TickHoldDeathPose(Time.deltaTime)
                ? Status.Running
                : Status.Success;
        }
    }
}
