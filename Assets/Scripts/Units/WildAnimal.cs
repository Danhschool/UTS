using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Unit động vật hoang: AI qua Behavior Graph, không nhận lệnh người chơi.
    /// </summary>
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public class WildAnimal : AbstractUnit
    {
        [SerializeField] private AnimalAIConfigSO animalConfig;
        [Tooltip("Faction động vật — phải khác Player1 để unit player tấn công và vào NearbyEnemies.")]
        [SerializeField] private Owner wildlifeOwner = Owner.AI1;

        public AnimalAIConfigSO Config => animalConfig;

        protected override void Awake()
        {
            Owner = wildlifeOwner;
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            RefreshNearbyEnemies();
        }

        /// <summary>
        /// Mục tiêu: Cập nhật NearbyEnemies trên blackboard (dùng bởi EvaluateAnimalAICommand và Attack subgraph).
        /// Cách hoạt động: Lấy danh sách từ DamageableSensor, sort theo khoảng cách.
        /// </summary>
        public void RefreshNearbyEnemies()
        {
            if (graphAgent == null)
            {
                return;
            }

            UpdateNearbyEnemiesBlackboard();
        }

        public bool ShouldFlee()
        {
            if (MaxHealth <= 0)
            {
                return false;
            }

            return (float)CurrentHealth / MaxHealth <= animalConfig.FleeHealthFraction;
        }

        public bool RollEatThisTick()
        {
            return Random.value <= animalConfig.EatChancePerTick;
        }

    }
}
