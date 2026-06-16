using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Kiểm tra prefab unit khởi đầu PvE có UnitSO trước khi spawn.
    /// </summary>
    public static class PvAiStartingUnitPrefabValidator
    {
        /// <summary>
        /// Mục tiêu: Tránh spawn Worker(Clone) không có stats/lệnh do prefab thiếu UnitSO.
        /// Cách hoạt động: AbstractCommandable trên asset prefab phải có UnitSO gán trong Inspector.
        /// </summary>
        public static bool TryValidate(GameObject prefab, out string error)
        {
            error = null;
            if (prefab == null)
            {
                error = "unitPrefab null.";
                return false;
            }

            if (!prefab.TryGetComponent(out AbstractCommandable commandable))
            {
                error = $"Prefab {prefab.name} thiếu AbstractCommandable.";
                return false;
            }

            if (commandable.UnitSO != null)
            {
                return true;
            }

            error =
                $"Prefab {prefab.name} thiếu UnitSO (reference ScriptableObject hỏng hoặc chưa gán). "
                + "Trong Game 1 → PvAiGameSceneSetup → startingUnits: đổi sang Worker 1.prefab, "
                + "hoặc chạy menu ProjectRTS/Gameplay/★ Prepare Game 1 Scene.";
            return false;
        }
    }
}
