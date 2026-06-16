using GameDevTV.RTS.Environment;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Map <see cref="SupplySO"/> → <see cref="SupplyGainKind"/> cho Rpc/UI gather.
    /// </summary>
    public static class SupplyGainKindResolver
    {
        /// <summary>
        /// Mục tiêu: Server/client cùng asset — suy loại từ tên asset (Stone/Wood/Food).
        /// Cách hoạt động: So khớp tên không phân biệt hoa thường; không khớp → Unknown.
        /// </summary>
        public static SupplyGainKind Resolve(SupplySO supply)
        {
            if (supply == null)
            {
                return SupplyGainKind.Unknown;
            }

            string name = supply.name;
            if (name.Contains("Stone"))
            {
                return SupplyGainKind.Stone;
            }

            if (name.Contains("Wood"))
            {
                return SupplyGainKind.Wood;
            }

            if (name.Contains("Food"))
            {
                return SupplyGainKind.Food;
            }

            return SupplyGainKind.Unknown;
        }
    }
}
