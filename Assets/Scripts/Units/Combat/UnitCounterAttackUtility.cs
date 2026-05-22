using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Kích hoạt phản công vùng khi lính bị địch đánh — không qua lệnh UI/AI.
    /// </summary>
    public static class UnitCounterAttackUtility
    {
        /// <summary>
        /// Mục tiêu: Bật sweep phản công quanh unit khi nhận sát thương từ phe khác.
        /// Cách hoạt động: Bỏ qua nếu đã khóa đúng attacker; gọi <see cref="UnitAreaRampageController.BeginCounterAttack"/>.
        /// </summary>
        public static void TryRespondToHostileAttack(AbstractUnit unit, IDamageable attacker)
        {
            if (unit == null
                || unit.CurrentHealth <= 0
                || unit is not IAttacker
                || attacker == null
                || attacker.CurrentHealth <= 0
                || attacker.Owner == unit.Owner)
            {
                return;
            }

            if (unit.TryGetLiveAttackTarget(out IDamageable locked) && locked == attacker)
            {
                return;
            }

            UnitAreaRampageController rampage = unit.GetComponent<UnitAreaRampageController>();
            if (rampage == null)
            {
                rampage = unit.gameObject.AddComponent<UnitAreaRampageController>();
            }

            rampage.BeginCounterAttack(attacker);
        }
    }
}
