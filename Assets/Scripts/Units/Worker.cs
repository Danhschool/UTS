using System;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, ITransportable
    {
        public bool IsBuilding => graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> command) && command.Value == UnitCommands.BuildBuilding;
        public bool HasSupplies
        {
            get
            {
                if (graphAgent != null && graphAgent.GetVariable("SupplyAmountHeld", out BlackboardVariable<int> heldVariable))
                {
                    return heldVariable.Value > 0;
                }

                return false;
            }
        }
        public int TransportCapacityUsage => unitSO.TransportConfig.GetTransportCapacityUsage();
        [SerializeField] private BaseCommand CancelBuildingCommand;

        protected override void Start()
        {
            base.Start();
            if (graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> eventChannelVariable))
            {
                eventChannelVariable.Value.Event += HandleGatherSupplies;
            }
            if (graphAgent.GetVariable("BuildingEventChannel", out BlackboardVariable<BuildingEventChannel> buildingEventChannelVariable))
            {
                buildingEventChannelVariable.Value.Event += HandleBuildingEvent;
            }
        }

        public void LoadInto(ITransporter transporter)
        {
            PauseActiveConstructionIfNeeded();
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }

        public void Gather(GatherableSupply supply)
        {
            PauseActiveConstructionIfNeeded();
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Gather);
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            PauseActiveConstructionIfNeeded();
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue("CommandPost", commandPost);
            graphAgent.SetVariableValue("Command", UnitCommands.ReturnSupplies);
        }

        public override void MoveTo(Vector3 position)
        {
            PauseActiveConstructionIfNeeded();
            base.MoveTo(position);
        }

        public override void MoveTo(Transform transform)
        {
            PauseActiveConstructionIfNeeded();
            base.MoveTo(transform);
        }

        public override void Attack(IDamageable damageable)
        {
            PauseActiveConstructionIfNeeded();
            base.Attack(damageable);
        }

        public override void Attack(Vector3 location)
        {
            PauseActiveConstructionIfNeeded();
            base.Attack(location);
        }

        public override void Stop()
        {
            PauseActiveConstructionIfNeeded();
            base.Stop();
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            DisposeMovementDestinationCursor();
            if (building.Prefab != null && !building.Prefab.TryGetComponent(out BaseBuilding _))
            {
                Debug.LogError($"Missing BaseBuilding on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                return null;
            }

            GameObject ghostMarker = new GameObject("BuildPlacementMarker");
            ghostMarker.transform.SetPositionAndRotation(targetLocation, Quaternion.identity);

            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", targetLocation);
            graphAgent.SetVariableValue("Ghost", ghostMarker);
            graphAgent.SetVariableValue<BaseBuilding>("BuildingUnderConstruction", null);
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Stone, building.Cost.StoneSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Wood, building.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Food, building.Cost.FoodSO));

            return null;
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            if (building == null || !building.CanResumeConstruction())
            {
                return;
            }

            PauseActiveConstructionIfNeeded();

            // Không StartBuilding ở đây — graph (BuildBuildingAction) sẽ gọi khi vào nhánh xây; tránh ArrivedAt/BuildingIsInProgress trên graph chặn resume.
            DisposeMovementDestinationCursor();
            if (Agent != null)
            {
                Agent.enabled = true;
            }

            graphAgent.SetVariableValue("TargetLocation", building.transform.position);
            graphAgent.SetVariableValue("BuildingUnderConstruction", building);
            graphAgent.SetVariableValue("BuildingSO", building.BuildingSO);
            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);
            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
        }

        /// <summary>
        /// Mục tiêu: Khi worker nhận lệnh khác giữa chừng xây, tạm dừng công trình và bật lại NavMesh (không Destroy nhà).
        /// Cách hoạt động: Nếu blackboard có BuildingUnderConstruction đang Building thì gọi Pause trên site, clear ref, bật agent.
        /// </summary>
        private void PauseActiveConstructionIfNeeded()
        {
            if (!graphAgent.GetVariable("BuildingUnderConstruction", out BlackboardVariable<BaseBuilding> siteVariable)
                || siteVariable.Value == null)
            {
                return;
            }

            BaseBuilding site = siteVariable.Value;
            if (site != null && site.Progress.State == BuildingProgress.BuildingState.Building)
            {
                site.PauseConstructionAndReleaseBuilder();
            }

            if (Agent != null)
            {
                Agent.enabled = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Worker rời chế độ build (Cancel trên UI) mà không phá công trình đang dở.
        /// Cách hoạt động: Xóa marker Ghost nếu còn; pause site qua PauseActiveConstructionIfNeeded; Stop và bỏ override.
        /// </summary>
        public void CancelBuilding()
        {
            if (graphAgent.GetVariable("Ghost", out BlackboardVariable<GameObject> ghostVariable)
                && ghostVariable.Value != null)
            {
                Destroy(ghostVariable.Value);
                graphAgent.SetVariableValue<GameObject>("Ghost", null);
            }

            PauseActiveConstructionIfNeeded();

            SetCommandOverrides(Array.Empty<BaseCommand>());
            Stop();
        }

        public override void Deselect()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(false);
            }

            IsSelected = false;
            if (!IsBuilding)
            {
                SetCommandOverrides(null);
            }

            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        private void HandleGatherSupplies(GameObject self, int amount, SupplySO supply)
        {
            if (supply == null)
            {
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, amount, supply));
        }

        private void HandleBuildingEvent(GameObject self, BuildingEventType eventType, BaseBuilding building)
        {
            switch(eventType)
            {
                case BuildingEventType.ArrivedAt:
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Begin:
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Cancel:
                case BuildingEventType.Abort:
                case BuildingEventType.Completed:
                    SetCommandOverrides(null);
                    break;
                default:
                    break;
            }
        }
    }
}
