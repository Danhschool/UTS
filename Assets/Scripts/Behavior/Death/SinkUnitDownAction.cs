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
        name: "Sink Unit Down",
        story: "[Self] sinks the unit downward into the ground.",
        category: "Action/Units/Death",
        id: "b9d3f6e7580a9c123d4e5f6a0b1c2d5")]
    public partial class SinkUnitDownAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        private UnitDeathController deathController;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out deathController))
            {
                return Status.Failure;
            }

            deathController.BeginSink();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (deathController == null)
            {
                return Status.Success;
            }

            return deathController.TickSink(Time.deltaTime)
                ? Status.Running
                : Status.Success;
        }
    }
}
