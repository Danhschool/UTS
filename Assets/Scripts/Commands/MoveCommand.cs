using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Move Action", menuName = "Units/Commands/Move", order = 100)]
    public class MoveCommand : BaseCommand
    {
        [SerializeField] private float radiusMultiplier = 3.5f;

        /// <summary>Khoảng cách giữa các ô formation vuông (bội số bán kính agent).</summary>
        public float FormationSpacingMultiplier => radiusMultiplier;

        public override bool CanHandle(CommandContext context) =>
            context.Commandable is AbstractUnit;

        public override void Handle(CommandContext context)
        {
            AbstractUnit unit = (AbstractUnit)context.Commandable;

            if (context.Hit.collider != null
                && context.Hit.collider.TryGetComponent(out AbstractCommandable commandable)
                && commandable.IsVisible)
            {
                unit.MoveTo(commandable.transform);
                return;
            }

            unit.MoveTo(context.Hit.point);
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}