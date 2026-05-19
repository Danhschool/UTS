using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Research Upgrade", menuName = "Tech Tree/Research Upgrade Command", order = 140)]
    public class ResearchUpgradeCommand : BaseCommand
    {
        [field: SerializeField] public UpgradeSO Upgrade { get; private set; }

        public override bool CanHandle(CommandContext context) => context.Commandable is BaseBuilding;

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding building)
            {
                return;
            }

            if (!CanResearch(building, context) || !HasEnoughSupplies(context))
            {
                return;
            }

            building.BuildUnlockable(Upgrade);
        }

        public override bool IsLocked(CommandContext context)
        {
            if (!HasEnoughSupplies(context) || !Upgrade.TechTree.IsUnlocked(context.Owner, Upgrade))
            {
                return true;
            }

            return context.Commandable is BaseBuilding building && !building.CanEnqueueUpgrade(Upgrade);
        }

        public override bool IsAvailable(CommandContext context)
        {
            if (Upgrade.IsOneTimeUnlock && Upgrade.TechTree.IsResearched(context.Owner, Upgrade))
            {
                return false;
            }

            if (context.Commandable is BaseBuilding building && !building.CanEnqueueUpgrade(Upgrade))
            {
                return false;
            }

            return Upgrade.TechTree.IsUnlocked(context.Owner, Upgrade);
        }

        private bool CanResearch(BaseBuilding building, CommandContext context) =>
            building.CanEnqueueUpgrade(Upgrade)
            && Upgrade.TechTree.IsUnlocked(context.Owner, Upgrade)
            && (!Upgrade.IsOneTimeUnlock || !Upgrade.TechTree.IsResearched(context.Owner, Upgrade));

        private bool HasEnoughSupplies(CommandContext context) =>
            Upgrade.Cost.Stone <= Supplies.Stone[context.Owner]
            && Upgrade.Cost.Wood <= Supplies.Wood[context.Owner]
            && Upgrade.Cost.Food <= Supplies.Food[context.Owner];
    }
}
