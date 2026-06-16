using System.Collections;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    public class BuildingUnderConstructionUI : MonoBehaviour, IUIElement<BaseBuilding>
    {
        [SerializeField] private TextMeshProUGUI unitName;
        [SerializeField] private ProgressBar progressBar;

        Coroutine progressRoutine;

        public void EnableFor(BaseBuilding building)
        {
            if (building == null)
            {
                Disable();
                return;
            }

            gameObject.SetActive(true);

            if (unitName != null)
            {
                unitName.SetText(building.UnitSO != null ? building.UnitSO.Name : building.name);
            }

            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
            }

            progressRoutine = StartCoroutine(AnimateBuildingProgress(building));
        }

        public void Disable()
        {
            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
                progressRoutine = null;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Mục tiêu: Progress bar xây nhà trên client MP — dùng Completion từ SyncVar khi có.
        /// Cách hoạt động: Ưu tiên Progress.Completion; fallback tính theo StartTime + BuildTime.
        /// </summary>
        IEnumerator AnimateBuildingProgress(BaseBuilding building)
        {
            while (enabled && building != null)
            {
                BuildingProgress progress = building.Progress;

                if (progress.State == BuildingProgress.BuildingState.Completed)
                {
                    progressBar?.SetProgress(1f);
                    yield break;
                }

                if (progress.State == BuildingProgress.BuildingState.Building
                    && progressBar != null)
                {
                    float value = progress.Completion > 0f
                        ? Mathf.Clamp01(progress.Completion)
                        : ComputeTimeBasedProgress(building, progress);
                    progressBar.SetProgress(value);
                }

                yield return null;
            }
        }

        static float ComputeTimeBasedProgress(BaseBuilding building, BuildingProgress progress)
        {
            if (building.BuildingSO == null)
            {
                return 0f;
            }

            float startTime = progress.StartTime;
            float duration = building.BuildingSO.BuildTime;
            if (duration <= 0.01f)
            {
                return 0f;
            }

            return Mathf.Clamp01((Time.time - startTime) / duration);
        }
    }
}
