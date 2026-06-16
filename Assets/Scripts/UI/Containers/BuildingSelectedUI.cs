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

        BaseBuilding selectedBuilding;
        Owner subscribedBusOwner = Owner.Invalid;

        public void EnableFor(BaseBuilding building)
        {
            if (building == null)
            {
                Disable();
                return;
            }

            if (selectedBuilding != null)
            {
                selectedBuilding.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            selectedBuilding = building;
            selectedBuilding.OnQueueUpdated += OnBuildingQueueUpdated;
            ApplyPresentationState();
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

        void OnBuildingQueueUpdated(UnlockableSO[] _ = null) => ApplyPresentationState();

        /// <summary>
        /// Mục tiêu: Panel nhà giống host — construction progress, train queue, hoặc single-unit info.
        /// Cách hoạt động: Ưu tiên Progress chưa Completed; sau đó queue hoặc stats panel.
        /// </summary>
        void ApplyPresentationState()
        {
            if (selectedBuilding == null)
            {
                return;
            }

            if (selectedBuilding.Progress.State != BuildingProgress.BuildingState.Completed)
            {
                buildingUnderConstructionUI.EnableFor(selectedBuilding);
                buildingBuildingUI.Disable();
                singleUnitSelectedUI.Disable();
                SubscribeBuildingSpawnBus();
                return;
            }

            buildingUnderConstructionUI.Disable();
            UnsubscribeBuildingSpawnBus();

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

        void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == selectedBuilding)
            {
                UnsubscribeBuildingSpawnBus();
                ApplyPresentationState();
            }
        }
    }
}
