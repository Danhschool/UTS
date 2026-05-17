using GameDevTV.RTS.Units;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace GameDevTV.RTS.Behavior.Animal
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Spawn Corpse Food Supply",
        story: "[Self] spawns [CorpsePrefab] (preconfigured gatherable supply) at its position.",
        category: "Action/Animal",
        id: "c3d4e5f60718293a4b5c6d7e8f901234")]
    public partial class SpawnCorpseFoodSupplyAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> CorpsePrefab;

        /// <summary>
        /// Mục tiêu: Spawn xác là prefab supply đã cấu hình sẵn (GatherableSupply + SupplySO trên prefab).
        /// Cách hoạt động: Lấy prefab từ blackboard hoặc AnimalAIConfigSO, Instantiate tại vị trí animal.
        /// </summary>
        protected override Status OnStart()
        {
            if (Self.Value == null)
            {
                return Status.Failure;
            }

            GameObject prefab = CorpsePrefab.Value;
            WildAnimal animal = Self.Value.GetComponent<WildAnimal>()
                ?? Self.Value.GetComponentInParent<WildAnimal>()
                ?? Self.Value.GetComponentInChildren<WildAnimal>(true);

            if (animal != null)
            {
                return animal.TrySpawnCorpseFoodSupply() ? Status.Success : Status.Failure;
            }

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(SpawnCorpseFoodSupplyAction)} on '{Self.Value.name}': missing WildAnimal and corpse prefab.",
                    Self.Value);
                return Status.Failure;
            }

            Vector3 spawnPosition = Self.Value.transform.position;
            Quaternion spawnRotation = Quaternion.Euler(0f, Self.Value.transform.eulerAngles.y, 0f);
            UnityEngine.Object.Instantiate(prefab, spawnPosition, spawnRotation);
            return Status.Success;
        }
    }
}
