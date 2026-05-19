using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Gather Action", menuName = "Units/Commands/Gather", order = 105)]
    public class GatherCommand : BaseCommand
    {
        [SerializeField] private BuildingSO storeBuildingSO;
        [SerializeField] private BuildingSO mainBuildingSO;

        private readonly List<AbstractUnitSO> configuredDepositTypes = new();

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is Worker
                && context.Hit.collider != null
                && IsGatherableSupplyOrDepositBuilding(context.Hit.collider);
        }

        public override void Handle(CommandContext context)
        {
            Worker worker = context.Commandable as Worker;
            if (!IsHitColliderVisible(context))
            {
                worker.MoveTo(context.Hit.collider.gameObject.transform.position);
            }
            else if (context.Hit.collider.TryGetComponent(out GatherableSupply supply))
            {
                worker.Gather(supply);
            }
            else if (IsSupplyDepositBuilding(context.Hit.collider) && worker.HasSupplies)
            {
                worker.ReturnSupplies(context.Hit.collider.gameObject);
            }
            else
            {
                worker.MoveTo(context.Hit.collider.gameObject.transform.position);
            }
        }

        public override bool IsLocked(CommandContext context) => false;

        private bool IsGatherableSupplyOrDepositBuilding(Collider collider) =>
            collider.TryGetComponent(out GatherableSupply _) || IsSupplyDepositBuilding(collider);

        private bool IsSupplyDepositBuilding(Collider collider)
        {
            if (!collider.TryGetComponent(out BaseBuilding building))
            {
                return false;
            }

            SupplyDepositLocator.CollectConfiguredTypes(storeBuildingSO, mainBuildingSO, configuredDepositTypes);
            return SupplyDepositLocator.IsSupplyDeposit(building, configuredDepositTypes);
        }
    }
}
