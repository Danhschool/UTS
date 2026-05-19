using System.Collections;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Buildings
{
    /// <summary>
    /// Periodically adds food to the building owner's supply pool when the structure is operational.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class PassiveFoodGeneratorBuilding : MonoBehaviour, IBuildingPassiveEffect
    {
        [SerializeField] private PassiveFoodGeneratorConfigSO config;
        [SerializeField] private BaseBuilding building;

        private Coroutine generationRoutine;

        private void Awake()
        {
            if (building == null)
            {
                building = GetComponent<BaseBuilding>();
            }

            enabled = false;
        }

        public void SetEffectActive(bool isActive)
        {
            enabled = isActive;
        }

        private void OnEnable()
        {
            if (config == null)
            {
                Debug.LogError($"[{nameof(PassiveFoodGeneratorBuilding)}] Missing config on {name}.", this);
                return;
            }

            if (config.FoodSupply == null)
            {
                Debug.LogError($"[{nameof(PassiveFoodGeneratorBuilding)}] FoodSupply not set on config '{config.name}'.", this);
                return;
            }

            generationRoutine = StartCoroutine(GenerateFoodRoutine());
        }

        private void OnDisable()
        {
            if (generationRoutine != null)
            {
                StopCoroutine(generationRoutine);
                generationRoutine = null;
            }
        }

        private IEnumerator GenerateFoodRoutine()
        {
            while (enabled)
            {
                if (!BuildingEffectUtility.IsOperational(building))
                {
                    yield return null;
                    continue;
                }

                yield return new WaitForSeconds(config.IntervalSeconds);

                if (!BuildingEffectUtility.IsOperational(building))
                {
                    continue;
                }

                GrantFood();
            }
        }

        /// <summary>
        /// Mục tiêu: Cộng food cho phe sở hữu nhà (Field, farm, …).
        /// Cách hoạt động: Raise <see cref="SupplyEvent"/> với amount dương qua event bus giống worker gather.
        /// </summary>
        private void GrantFood()
        {
            Bus<SupplyEvent>.Raise(
                building.Owner,
                new SupplyEvent(building.Owner, config.FoodPerTick, config.FoodSupply));
        }
    }
}
