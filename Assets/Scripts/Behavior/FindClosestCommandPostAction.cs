using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections.Generic;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;

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

            Vector3 fromPosition = Unit.Value.transform.position;
            if (!SupplyDepositLocator.TryFindClosest(
                    fromPosition,
                    SearchRadius.Value,
                    workerUnit.Owner,
                    configuredDepositTypes,
                    out BaseBuilding closest))
            {
                if (RtsNetplaySession.IsNetworkMatch && NetworkServer.active)
                {
                    Debug.LogWarning(
                        $"[FindClosestCommandPost] Không tìm thấy deposit cho {workerUnit.name} "
                        + $"(owner={workerUnit.Owner}, radius={SearchRadius.Value}).");
                }

                return Status.Failure;
            }

            CommandPost.Value = closest.gameObject;

            Vector3 approach = SupplyDepositApproachUtility.ResolveApproachPosition(
                fromPosition,
                closest.gameObject,
                Unit.Value.GetInstanceID());

            if (Unit.Value.TryGetComponent(out BehaviorGraphAgent graphAgent))
            {
                graphAgent.SetVariableValue("TargetLocation", approach);
            }

            if (RtsNetplaySession.IsNetworkMatch
                && NetworkServer.active
                && Unit.Value.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                && closest.TryGetComponent(out NetworkIdentity commandPostIdentity))
            {
                if (Unit.Value.TryGetComponent(out Worker worker))
                {
                    worker.ApplyReturnToDepositState(approach, closest.gameObject);
                    worker.PushServerReturnNavigation(approach);
                    worker.BeginServerReturnDepositWatch(approach, closest.gameObject);
                }

                networkEntity.RpcMirrorReturnPresentation(approach, commandPostIdentity.netId);
            }

            return Status.Success;
        }
    }
}
