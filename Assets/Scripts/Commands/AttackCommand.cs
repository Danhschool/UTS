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

            // Right-click: chỉ địch có Owner khác unit đang ra lệnh (không “attack” đồng minh / chính mình).
            if (context.Button == UnityEngine.InputSystem.LowLevel.MouseButton.Right)
            {
                if (!context.Hit.collider.TryGetComponent(out IDamageable damageable) || !IsHitColliderVisible(context))
                {
                    return false;
                }

                return damageable.Owner != context.Commandable.Owner;
            }

            return true;
        }

        public override void Handle(CommandContext context)
        {
            IAttacker attacker = context.Commandable as IAttacker;
            if (context.Hit.collider.TryGetComponent(out IDamageable damageable) && IsHitColliderVisible(context))
            {
                if (damageable.Owner != context.Commandable.Owner)
                {
                    attacker.Attack(damageable);
                }
                else
                {
                    attacker.Attack(context.Hit.point);
                }
            }
            else
            {
                attacker.Attack(context.Hit.point);
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}