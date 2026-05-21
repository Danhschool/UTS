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
            Collider collider = context.Hit.collider;
            if (collider == null)
            {
                return;
            }

            if (IsSupplyDepositBuilding(collider) && worker.HasSupplies)
            {
                worker.ReturnSupplies(collider.gameObject);
                return;
            }

            GatherableSupply supply = collider.GetComponentInParent<GatherableSupply>();
            if (supply != null && supply.Amount > 0)
            {
                if (IsHitColliderVisible(context))
                {
                    worker.Gather(supply);
                }
                else
                {
                    worker.MoveTo(supply.transform.position);
                }

                return;
            }

            worker.MoveTo(collider.gameObject.transform.position);
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
