using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Panel các ô stat khi chọn một unit (Damage, tốc chạy, tốc đánh, tầm…).
    /// </summary>
    public class UnitStatsPanelUI : MonoBehaviour
    {
        [Header("Slots (kéo instance Armor Damage Icon đã đặt tên)")]
        [SerializeField] private UnitStatSlotUI damageSlot;
        [SerializeField] private UnitStatSlotUI attackSpeedSlot;
        [SerializeField] private UnitStatSlotUI attackRangeSlot;
        [SerializeField] private UnitStatSlotUI moveSpeedSlot;
        [SerializeField] private UnitStatSlotUI armorSlot;

        [Header("Upgrade — PropertyPath khớp UpgradeSO (.asset)")]
        [SerializeField] private string damageUpgradePath = "AttackConfig/Damage";
        [SerializeField] private string attackSpeedUpgradePath = "AttackConfig/AttackDelay";
        [SerializeField] private string attackRangeUpgradePath = "AttackConfig/AttackRange";
        [SerializeField] private string moveSpeedUpgradePath = "MoveSpeed";

        /// <summary>
        /// Mục tiêu: Điền giá trị stat và level upgrade lên từng ô prefab.
        /// Cách hoạt động: Đọc UnitSO runtime; đếm upgrade đã research theo PropertyPath; tooltip/icon cấu hình trên từng UnitStatSlotUI.
        /// </summary>
        public void Bind(AbstractCommandable commandable)
        {
            HideAllSlots();

            if (commandable?.UnitSO == null)
            {
                return;
            }

            if (commandable.UnitSO is UnitSO unitSo)
            {
                Owner owner = commandable.Owner;
                moveSpeedSlot?.Set(
                    FormatOneDecimal(unitSo.MoveSpeed),
                    CountResearchedUpgrades(unitSo, owner, moveSpeedUpgradePath),
                    GetCurrentUpgradeIcon(unitSo, owner, moveSpeedUpgradePath));

                if (unitSo.AttackConfig != null)
                {
                    AttackConfigSO attack = unitSo.AttackConfig;
                    damageSlot?.Set(
                        attack.Damage.ToString(),
                        CountResearchedUpgrades(unitSo, owner, damageUpgradePath),
                        GetCurrentUpgradeIcon(unitSo, owner, damageUpgradePath));
                    attackSpeedSlot?.Set(
                        FormatAttackSpeed(attack.AttackDelay),
                        CountResearchedUpgrades(unitSo, owner, attackSpeedUpgradePath),
                        GetCurrentUpgradeIcon(unitSo, owner, attackSpeedUpgradePath));
                    attackRangeSlot?.Set(
                        FormatOneDecimal(attack.AttackRange),
                        CountResearchedUpgrades(unitSo, owner, attackRangeUpgradePath),
                        GetCurrentUpgradeIcon(unitSo, owner, attackRangeUpgradePath));
                }
            }

            armorSlot?.Hide();
        }

        public void HideAllSlots()
        {
            damageSlot?.Hide();
            attackSpeedSlot?.Hide();
            attackRangeSlot?.Hide();
            moveSpeedSlot?.Hide();
            armorSlot?.Hide();
        }

        /// <summary>
        /// Mục tiêu: Đếm số upgrade đã research ảnh hưởng stat (hiển thị ở Upgrade Count Text).
        /// Cách hoạt động: So khớp chính xác PropertyPath (vd. AttackConfig/Damage, MoveSpeed).
        /// </summary>
        private static int CountResearchedUpgrades(UnitSO unitSo, Owner owner, string propertyPath)
        {
            if (unitSo.Upgrades == null || unitSo.TechTree == null || string.IsNullOrEmpty(propertyPath))
            {
                return 0;
            }

            int count = 0;
            foreach (UpgradeSO upgrade in unitSo.Upgrades)
            {
                if (upgrade == null)
                {
                    continue;
                }

                if (!PropertyPathMatches(upgrade.PropertyPath, propertyPath))
                {
                    continue;
                }

                if (unitSo.TechTree.IsResearched(owner, upgrade))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool PropertyPathMatches(string upgradePath, string expectedPath)
        {
            if (string.IsNullOrEmpty(upgradePath) || string.IsNullOrEmpty(expectedPath))
            {
                return false;
            }

            return string.Equals(
                NormalizeUpgradePropertyPath(upgradePath),
                NormalizeUpgradePropertyPath(expectedPath),
                System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Mục tiêu: Khớp PropertyPath dù Inspector/scene ghi dạng rút gọn (vd. "Damage").
        /// Cách hoạt động: Map tên cũ → path đầy đủ khớp UpgradeSO (.asset).
        /// </summary>
        private static string NormalizeUpgradePropertyPath(string path)
        {
            return path switch
            {
                "Damage" => "AttackConfig/Damage",
                "AttackDelay" => "AttackConfig/AttackDelay",
                "AttackRange" => "AttackConfig/AttackRange",
                _ => path
            };
        }

        /// <summary>
        /// Mục tiêu: Lấy icon của upgrade đã research cao nhất cho stat này.
        /// Cách hoạt động: Duyệt Upgrades theo thứ tự mảng; giữ Icon của bản researched cuối cùng khớp PropertyPath.
        /// </summary>
        private static Sprite GetCurrentUpgradeIcon(UnitSO unitSo, Owner owner, string propertyPath)
        {
            if (unitSo.Upgrades == null || unitSo.TechTree == null || string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            Sprite latestIcon = null;
            foreach (UpgradeSO upgrade in unitSo.Upgrades)
            {
                if (upgrade == null || !PropertyPathMatches(upgrade.PropertyPath, propertyPath))
                {
                    continue;
                }

                if (!unitSo.TechTree.IsResearched(owner, upgrade))
                {
                    continue;
                }

                if (upgrade.Icon != null)
                {
                    latestIcon = upgrade.Icon;
                }
            }

            return latestIcon;
        }

        private static string FormatOneDecimal(float value) => value.ToString("0.#");

        private static string FormatAttackSpeed(float attackDelaySeconds)
        {
            if (attackDelaySeconds <= 0.01f)
            {
                return "—";
            }

            float attacksPerSecond = 1f / attackDelaySeconds;
            return $"{attacksPerSecond:0.#}/s";
        }
    }
}
