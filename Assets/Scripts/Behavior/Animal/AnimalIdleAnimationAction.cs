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
        name: "Animal Idle Animation",
        story: "[Self] stays idle for [Duration] seconds (Stop command).",
        category: "Action/Animal",
        id: "d4e5f60718293a4b5c6d7e8f9012345")]
    public partial class AnimalIdleAnimationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<float> Duration = new(3f);

        private float timer;
        private Animator animator;

        /// <summary>
        /// Mục tiêu: Giữ trạng thái idle trên graph đủ lâu trước khi Evaluate chọn lệnh mới.
        /// Cách hoạt động: Tắt bool di chuyển/ăn/đánh, chờ Duration từ AnimalAIConfig, rồi Success.
        /// </summary>
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
                Duration.Value = animal.Config.IdleDurationSeconds;
            }

            animator.SetBool(AnimationConstants.IS_MOVING, false);
            animator.SetBool(AnimationConstants.IS_ATTACK, false);
            animator.SetBool(AnimationConstants.IS_FLEEING, false);
            animator.SetBool(AnimationConstants.IS_EATING, false);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            timer += Time.deltaTime;
            return timer >= Duration.Value ? Status.Success : Status.Running;
        }
    }
}
