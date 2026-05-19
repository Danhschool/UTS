using UnityEngine;

namespace GameDevTV.RTS.Units.Visualization
{
    /// <summary>
    /// Gắn <see cref="UnitAttackRangeDisplay"/> cho unit có tầm đánh (không áp dụng building).
    /// </summary>
    public static class AttackRangeDisplayInstaller
    {
        public static void InstallIfNeeded(GameObject host)
        {
            if (host == null
                || host.GetComponent<BaseBuilding>() != null
                || host.GetComponent<UnitAttackRangeDisplay>() != null)
            {
                return;
            }

            AbstractUnit unit = host.GetComponent<AbstractUnit>();
            if (unit == null
                || unit.UnitSO is not UnitSO unitSo
                || unitSo.AttackConfig == null
                || unitSo.AttackConfig.AttackRange <= 0f)
            {
                return;
            }

            host.AddComponent<UnitAttackRangeDisplay>();
        }
    }
}
