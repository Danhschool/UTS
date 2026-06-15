using System.Collections.Generic;
using GameDevTV.RTS.Units.Combat;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class BaseMilitaryUnit : AbstractUnit, ITransportable
    {
        public int TransportCapacityUsage => unitSO.TransportConfig.GetTransportCapacityUsage();

        protected override void Awake()
        {
            base.Awake();
            EnsureCounterAttackController();
        }

        protected override void Start()
        {
            base.Start();
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
        }

        /// <summary>
        /// Mục tiêu: Sensor/BT thấy địch — ưu tiên lính địch gần nhất trước nhà.
        /// </summary>
        protected override List<GameObject> UpdateNearbyEnemiesBlackboard()
        {
            List<GameObject> nearbyEnemies = base.UpdateNearbyEnemiesBlackboard();
            CombatTargetPriorityUtility.SortGameObjectsByUnitPriority(
                nearbyEnemies,
                transform.position);
            graphAgent.SetVariableValue("NearbyEnemies", nearbyEnemies);
            return nearbyEnemies;
        }

        /// <summary>
        /// Mục tiêu: Bị địch đánh → phản công vùng nội bộ; AI chủ động vẫn dùng AttackCommand riêng.
        /// Cách hoạt động: Sau khi trừ máu, gọi <see cref="UnitCounterAttackUtility.TryRespondToHostileAttack"/>.
        /// </summary>
        protected override void TakeDamageWithSideEffects(int damage, IDamageable attacker)
        {
            base.TakeDamageWithSideEffects(damage, attacker);

            if (CurrentHealth > 0)
            {
                UnitCounterAttackUtility.TryRespondToHostileAttack(this, attacker);
            }
        }

        private void EnsureCounterAttackController()
        {
            if (GetComponent<UnitAreaRampageController>() == null)
            {
                gameObject.AddComponent<UnitAreaRampageController>();
            }
        }

        public void LoadInto(ITransporter transporter)
        {
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }
    }
}