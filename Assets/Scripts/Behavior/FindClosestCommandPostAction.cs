using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Find Closest Supply Deposit",
        story: "[Unit] finds nearest supply deposit (store or main base) into [CommandPost].",
        category: "Action/Units",
        id: "df019f9861776b3b31754a035175faf5")]
    public partial class FindClosestCommandPostAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Unit;
        [SerializeReference] public BlackboardVariable<GameObject> CommandPost;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(10);
        [SerializeReference] public BlackboardVariable<BuildingSO> CommandPostBuilding;
        [SerializeReference] public BlackboardVariable<BuildingSO> StoreBuilding;
        [SerializeReference] public BlackboardVariable<BuildingSO> MainBuilding;

        private readonly List<AbstractUnitSO> configuredDepositTypes = new();

        protected override Status OnStart()
        {
            if (Unit.Value == null)
            {
                return Status.Failure;
            }

            if (!Unit.Value.TryGetComponent(out AbstractUnit workerUnit))
            {
                return Status.Failure;
            }

            SupplyDepositLocator.CollectConfiguredTypes(
                StoreBuilding != null ? StoreBuilding.Value : null,
                MainBuilding != null ? MainBuilding.Value : null,
                configuredDepositTypes);

            if (!SupplyDepositLocator.TryFindClosest(
                    Unit.Value.transform.position,
                    SearchRadius.Value,
                    workerUnit.Owner,
                    configuredDepositTypes,
                    out BaseBuilding closest))
            {
                return Status.Failure;
            }

            CommandPost.Value = closest.gameObject;
            return Status.Success;
        }
    }
}
