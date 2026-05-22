using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Combat;
using GameDevTV.RTS.Units.Formation;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Hội quân: gọi mọi lính trong bán kính tới điểm (formation), sau đó có thể tấn công vùng.
    /// </summary>
    [CreateAssetMenu(fileName = "Rally Area", menuName = "Units/Commands/Rally Area", order = 97)]
    public class RallyAreaCommand : BaseCommand
    {
        [SerializeField] private float rallyGatherRadius = 50f;

        public float RallyGatherRadius => Mathf.Max(1f, rallyGatherRadius);

        public override bool CanHandle(CommandContext context) =>
            context.Commandable is BaseMilitaryUnit
            && context.Commandable.CurrentHealth > 0;

        public override void Handle(CommandContext context) =>
            TryApplyRally(context.Commandable as AbstractUnit, context.Hit, this);

        public override bool IsLocked(CommandContext context) => false;

        /// <summary>
        /// Mục tiêu: Di chuyển mọi lính trong bán kính <see cref="rallyGatherRadius"/> tới điểm hội quân.
        /// Cách hoạt động: Thu thập quân → formation Move quanh hit.point.
        /// </summary>
        public static bool TryApplyRally(AbstractUnit issuer, RaycastHit hit, RallyAreaCommand command)
        {
            if (issuer == null || command == null)
            {
                return false;
            }

            MoveCommand moveCommand = ResolveMoveCommand(issuer);
            if (moveCommand == null)
            {
                return false;
            }

            List<AbstractUnit> rallyUnits = new(32);
            MilitaryUnitLocator.CollectMilitaryInRadius(
                issuer.Owner,
                hit.point,
                command.RallyGatherRadius,
                rallyUnits);

            if (rallyUnits.Count == 0)
            {
                return false;
            }

            return GroupFormationMoveUtility.TryApplyMove(rallyUnits, hit, moveCommand);
        }

        private static MoveCommand ResolveMoveCommand(AbstractUnit unit)
        {
            List<BaseCommand> commands = AvailableCommandsResolver.GetFlattened(unit);
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i] is MoveCommand move)
                {
                    return move;
                }
            }

            return null;
        }
    }
}
