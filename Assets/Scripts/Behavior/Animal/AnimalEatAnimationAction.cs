using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace GameDevTV.RTS.Behavior.Animal
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Animal Eat Animation",
        story: "[Self] plays eat animation for [Duration] seconds (no supply logic).",
        category: "Action/Animal",
        id: "b2c3d4e5f60718293a4b5c6d7e8f9012")]
    public partial class AnimalEatAnimationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<float> Duration = new(3f);

        private float timer;
        private Animator animator;

        protected override Status OnStart()
        {
            timer = 0f;
            animator = null;

            if (Self.Value == null)
            {
                return Status.Failure;
            }

            if (!Self.Value.TryGetComponent(out animator))
            {
                return Status.Failure;
            }

            if (Self.Value.TryGetComponent(out WildAnimal animal) && animal.Config != null)
            {
                Duration.Value = animal.Config.EatDurationSeconds;
            }

            animator.SetBool(AnimationConstants.IS_MOVING, false);
            animator.SetBool(AnimationConstants.IS_ATTACK, false);
            animator.SetBool(AnimationConstants.IS_FLEEING, false);
            animator.SetBool(AnimationConstants.IS_EATING, true);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            timer += Time.deltaTime;
            if (timer >= Duration.Value)
            {
                if (animator != null)
                {
                    animator.SetBool(AnimationConstants.IS_EATING, false);
                }

                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_EATING, false);
            }
        }
    }
}
