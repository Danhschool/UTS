using System;

namespace GameDevTV.RTS.Units.Formation
{
    /// <summary>
    /// SRP: Phân loại unit vào hàng trước / hàng sau theo tên UnitSO.
    /// </summary>
    public static class UnitFormationRoleClassifier
    {
        /// <summary>
        /// Mục tiêu: Archer luôn hàng cuối; Warrior, Knight, Worker hàng trước.
        /// Cách hoạt động: So khớp không phân biệt hoa thường trên Name / asset name.
        /// </summary>
        public static UnitFormationRole GetRole(AbstractUnit unit)
        {
            if (unit?.UnitSO == null)
            {
                return UnitFormationRole.Frontline;
            }

            string displayName = unit.UnitSO.Name ?? string.Empty;
            string assetName = unit.UnitSO.name ?? string.Empty;

            if (ContainsIgnoreCase(displayName, "Archer") || ContainsIgnoreCase(assetName, "Archer"))
            {
                return UnitFormationRole.Backline;
            }

            return UnitFormationRole.Frontline;
        }

        private static bool ContainsIgnoreCase(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack)
            && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
