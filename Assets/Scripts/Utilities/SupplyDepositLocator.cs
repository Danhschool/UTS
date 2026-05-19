using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Locates completed buildings where workers can deposit gathered supplies.
    /// </summary>
    public static class SupplyDepositLocator
    {
        public const string StoreHouseDisplayName = "Store House";
        public const string CivilCentralDisplayName = "Civil Central";

        private static readonly int BuildingsLayerMask = LayerMask.GetMask("Buildings");

        /// <summary>
        /// Mục tiêu: Xác định building có phải điểm nộp tài nguyên (store / nhà chính) hay không.
        /// Cách hoạt động: So khớp UnitSO với danh sách cấu hình; nếu danh sách rỗng thì dùng tên mặc định Store House / Civil Central.
        /// </summary>
        public static bool IsSupplyDeposit(BaseBuilding building, IReadOnlyList<AbstractUnitSO> configuredTypes)
        {
            if (building == null || building.Progress.State != BuildingProgress.BuildingState.Completed)
            {
                return false;
            }

            AbstractUnitSO unitSo = building.UnitSO;
            if (unitSo == null)
            {
                return false;
            }

            if (configuredTypes != null)
            {
                for (int i = 0; i < configuredTypes.Count; i++)
                {
                    AbstractUnitSO type = configuredTypes[i];
                    if (type != null && unitSo.Equals(type))
                    {
                        return true;
                    }
                }
            }

            return IsDefaultDepositByDisplayName(unitSo);
        }

        /// <summary>
        /// Mục tiêu: Tìm điểm nộp tài nguyên gần nhất trong bán kính (cùng Owner).
        /// Cách hoạt động: OverlapSphere trên layer Buildings, lọc deposit hợp lệ, chọn khoảng cách nhỏ nhất.
        /// </summary>
        public static bool TryFindClosest(
            Vector3 fromPosition,
            float searchRadius,
            Owner owner,
            IReadOnlyList<AbstractUnitSO> configuredTypes,
            out BaseBuilding closest)
        {
            closest = null;

            if (searchRadius <= 0f)
            {
                return false;
            }

            Collider[] colliders = Physics.OverlapSphere(fromPosition, searchRadius, BuildingsLayerMask);
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].TryGetComponent(out BaseBuilding building))
                {
                    continue;
                }

                if (building.Owner != owner)
                {
                    continue;
                }

                if (!IsSupplyDeposit(building, configuredTypes))
                {
                    continue;
                }

                float sqrDistance = (building.transform.position - fromPosition).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                closest = building;
            }

            return closest != null;
        }

        public static bool IsDefaultDepositByDisplayName(AbstractUnitSO unitSo) =>
            unitSo != null
            && (unitSo.Name == StoreHouseDisplayName || unitSo.Name == CivilCentralDisplayName);

        public static void CollectConfiguredTypes(
            AbstractUnitSO storeType,
            AbstractUnitSO mainType,
            List<AbstractUnitSO> buffer)
        {
            buffer.Clear();
            if (storeType != null)
            {
                buffer.Add(storeType);
            }

            if (mainType != null && !buffer.Contains(mainType))
            {
                buffer.Add(mainType);
            }
        }
    }
}
