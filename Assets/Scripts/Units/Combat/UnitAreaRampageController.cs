using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Phản công vùng khi bị đánh — quét hostile trong bán kính; hết địch thì tắt (AI/planner dùng Attack thường).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitAreaRampageController : MonoBehaviour
    {
        [SerializeField] private float counterAttackRadius = 50f;

        private AbstractUnit unit;
        private bool isActive;
        private bool isCounterAttackMode;
        private Vector3 areaCenter;
        private float areaRadius;
        private IDamageable lastEngaged;

        public float CounterAttackRadius => Mathf.Max(1f, counterAttackRadius);

        private void Awake()
        {
            unit = GetComponent<AbstractUnit>();
        }

        private void LateUpdate()
        {
            if (!isActive || unit == null || unit.CurrentHealth <= 0)
            {
                return;
            }

            if (isCounterAttackMode)
            {
                areaCenter = unit.transform.position;
            }

            if (HasLiveAttackTarget())
            {
                return;
            }

            if (CombatAreaTargetQuery.TryFindClosestHostileInRadius(
                    unit,
                    areaCenter,
                    areaRadius,
                    lastEngaged,
                    out IDamageable next))
            {
                lastEngaged = next;
                unit.Attack(next);
                return;
            }

            lastEngaged = null;
            if (isCounterAttackMode)
            {
                EndRampage();
                return;
            }

            unit.Attack(areaCenter);
        }

        /// <summary>
        /// Mục tiêu: Phản công sau khi bị đánh — ưu tiên attacker, quét trong bán kính.
        /// Cách hoạt động: Tâm vùng = vị trí unit; tắt khi không còn hostile trong radius.
        /// </summary>
        public void BeginCounterAttack(IDamageable attacker)
        {
            isCounterAttackMode = true;
            areaCenter = unit != null ? unit.transform.position : Vector3.zero;
            areaRadius = CounterAttackRadius;
            isActive = true;
            lastEngaged = null;

            if (TryAttackIfHostile(attacker))
            {
                return;
            }

            TryEngageNextTarget();
        }

        public void EndRampage()
        {
            isActive = false;
            isCounterAttackMode = false;
            lastEngaged = null;
        }

        public bool IsRampageActive => isActive;

        public void TryEngageNextTarget()
        {
            if (!isActive || unit == null || unit.CurrentHealth <= 0)
            {
                return;
            }

            if (CombatAreaTargetQuery.TryFindClosestHostileInRadius(
                    unit,
                    areaCenter,
                    areaRadius,
                    lastEngaged,
                    out IDamageable next))
            {
                lastEngaged = next;
                unit.Attack(next);
            }
            else if (!isCounterAttackMode)
            {
                unit.Attack(areaCenter);
            }
        }

        private bool TryAttackIfHostile(IDamageable attacker)
        {
            if (attacker == null
                || attacker.CurrentHealth <= 0
                || unit == null
                || attacker.Owner == unit.Owner)
            {
                return false;
            }

            lastEngaged = attacker;
            unit.Attack(attacker);
            return true;
        }

        private bool HasLiveAttackTarget() =>
            unit != null && unit.TryGetLiveAttackTarget(out _);
    }
}
