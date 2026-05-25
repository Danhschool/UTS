using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingSelectedUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private BuildingBuildingUI buildingBuildingUI;
        [SerializeField] private BuildingUnderConstructionUI buildingUnderConstructionUI;

        private BaseBuilding selectedBuilding;
        private Owner subscribedBusOwner = Owner.Invalid;

        public void EnableFor(BaseBuilding building)
        {
            selectedBuilding = building;
            selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
            selectedBuilding.OnQueueUpdated += OnBuildingQueueUpdated;

            if (building.Progress.State == BuildingProgress.BuildingState.Completed)
            {
                buildingUnderConstructionUI.Disable();
                OnBuildingQueueUpdated();
            }
            else
            {
                buildingUnderConstructionUI.EnableFor(building);
                buildingBuildingUI.Disable();
                singleUnitSelectedUI.Disable();
                SubscribeBuildingSpawnBus();
            }
        }

        public void Disable()
        {
            buildingBuildingUI.Disable();
            singleUnitSelectedUI.Disable();
            buildingUnderConstructionUI.Disable();
            UnsubscribeBuildingSpawnBus();
            if (selectedBuilding != null)
            {
                selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
                selectedBuilding = null;
            }
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] _ = null)
        {
            if (selectedBuilding.QueueSize == 0)
            {
                singleUnitSelectedUI.EnableFor(selectedBuilding);
                buildingBuildingUI.Disable();
            }
            else
            {
                buildingBuildingUI.EnableFor(selectedBuilding);
                singleUnitSelectedUI.Disable();
            }
        }

        void SubscribeBuildingSpawnBus()
        {
            UnsubscribeBuildingSpawnBus();
            subscribedBusOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            Bus<BuildingSpawnEvent>.OnEvent[subscribedBusOwner] += HandleBuildingSpawn;
        }

        void UnsubscribeBuildingSpawnBus()
        {
            if (subscribedBusOwner == Owner.Invalid)
            {
                return;
            }

            Bus<BuildingSpawnEvent>.OnEvent[subscribedBusOwner] -= HandleBuildingSpawn;
            subscribedBusOwner = Owner.Invalid;
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == selectedBuilding)
            {
                UnsubscribeBuildingSpawnBus();
                OnBuildingQueueUpdated();
                buildingUnderConstructionUI.Disable();
            }
        }
    }
}
