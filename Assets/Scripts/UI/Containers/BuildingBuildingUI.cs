using System.Collections;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Netplay;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingBuildingUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private UIBuildQueueButton[] unitButtons;
        [SerializeField] private ProgressBar progressBar;

        private Coroutine buildCoroutine;
        private BaseBuilding building;

        public void EnableFor(BaseBuilding item)
        {
            if (buildCoroutine != null)
            {
                StopCoroutine(buildCoroutine);
                buildCoroutine = null;
            }

            if (building != null)
            {
                building.OnQueueUpdated -= HandleQueueUpdated;
            }

            progressBar?.SetProgress(0);
            gameObject.SetActive(true);
            building = item;
            building.OnQueueUpdated += HandleQueueUpdated;
            SetupUnitButtons();

            buildCoroutine = StartCoroutine(UpdateUnitProgress());
        }

        private void SetupUnitButtons()
        {
            int i = 0;
            for (; i < building.QueueSize; i++)
            {
                int index = i;
                unitButtons[i].EnableFor(building.Queue[i], () => building.CancelBuildingUnit(index));
            }
            for (; i < unitButtons.Length; i++)
            {
                unitButtons[i].Disable();
            }
        }

        public void Disable()
        {
            if (buildCoroutine != null)
            {
                StopCoroutine(buildCoroutine);
                buildCoroutine = null;
            }

            if (building != null)
            {
                building.OnQueueUpdated -= HandleQueueUpdated;
            }

            gameObject.SetActive(false);
            building = null;
        }

        private void HandleQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            if (unitsInQueue.Length == 1 && buildCoroutine == null)
            {
                buildCoroutine = StartCoroutine(UpdateUnitProgress());
            }

            if (building != null)
            {
                SetupUnitButtons();
            }
        }

        private IEnumerator UpdateUnitProgress()
        {
            while (building != null && building.QueueSize > 0)
            {
                float startTime = building.CurrentQueueStartTime;
                float buildTime = building.SOBeingBuilt != null ? building.SOBeingBuilt.BuildTime : 0f;
                float now = RtsNetplaySession.IsNetworkMatch ? (float)NetworkTime.time : Time.time;

                float progress = buildTime > 0.01f
                    ? Mathf.Clamp01((now - startTime) / buildTime)
                    : 0f;

                progressBar?.SetProgress(progress);
                yield return null;
            }

            buildCoroutine = null;
        }
    }
}
