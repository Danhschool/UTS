using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Attack", menuName = "Units/Commands/Attack", order = 99)]
    public class AttackCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            if (context.Commandable is not IAttacker || context.Hit.collider == null)
            {
                return false;
            }

            // Right-click dispatch should only pick attack when clicking a valid visible damageable target.
            if (context.Button == UnityEngine.InputSystem.LowLevel.MouseButton.Right)
            {
                return context.Hit.collider.TryGetComponent(out IDamageable _) && IsHitColliderVisible(context);
            }

            return true;
        }

        public override void Handle(CommandContext context)
        {
            IAttacker attacker = context.Commandable as IAttacker;
            if (context.Hit.collider.TryGetComponent(out IDamageable damageable) && IsHitColliderVisible(context))
            {
                attacker.Attack(damageable);
            }
            else
            {
                attacker.Attack(context.Hit.point);
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}