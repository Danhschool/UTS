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
        name: "Wait Unit Death Animation",
        story: "[Self] waits until death clip ends or animation event fires.",
        category: "Action/Units/Death",
        id: "e6a0c3b425d7698f1a2b3c4d5e6f7b82")]
    public partial class WaitUnitDeathAnimationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;

        private UnitDeathController deathController;

        protected override Status OnStart()
        {
            if (!DeathBehaviorNodeUtility.TryGetDeathController(Self.Value, out deathController))
            {
                return Status.Failure;
            }

            deathController.BeginWaitDeathAnimation();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (deathController == null)
            {
                return Status.Success;
            }

            return deathController.TickWaitDeathAnimation(Time.deltaTime)
                ? Status.Running
                : Status.Success;
        }
    }
}
