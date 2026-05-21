using GameDevTV.RTS.Commands;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Một loại quân trong cơ cấu Barrack — lệnh Build Unit + trọng số spawn.
    /// </summary>
    [System.Serializable]
    public sealed class AIMilitaryUnitMixSlot
    {
        [Tooltip("Build Unit SO (ví dụ Build Warrior.asset).")]
        [SerializeField] private BuildUnitCommand trainCommand;

        [Tooltip("Tỷ lệ mong muốn (ví dụ 50 / 30 / 20). Tổng 3 slot không cần = 100 — AI tự chuẩn hoá.")]
        [SerializeField] [Min(0f)] private float spawnWeight = 1f;

        public BuildUnitCommand TrainCommand => trainCommand;
        public float SpawnWeight => Mathf.Max(0f, spawnWeight);

        public bool IsAssigned => trainCommand != null && SpawnWeight > 0f;
    }
}
