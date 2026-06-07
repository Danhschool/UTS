using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition
{
    /// <summary>
    /// SRP: Một asset gom prefab nhà + unit cho voice — gán Inspector một lần, dễ tìm.
    /// </summary>
    [CreateAssetMenu(fileName = "VoiceCommandGameplayPrefabs", menuName = "ProjectRTS/Voice Command Gameplay Prefabs")]
    public sealed class VoiceCommandGameplayPrefabs : ScriptableObject
    {
        [Header("Tòa nhà trên map (prefab thật — không dùng ghost)")]
        [Tooltip("Civil Central — lệnh tạo dân.")]
        [SerializeField] GameObject civilCentralBuildingPrefab;

        [Tooltip("Store House / nhà kho.")]
        [SerializeField] GameObject storeHouseBuildingPrefab;

        [Tooltip("Corral / chuồng.")]
        [SerializeField] GameObject corralBuildingPrefab;

        [Tooltip("Forge — lệnh nâng cấp.")]
        [SerializeField] GameObject forgeBuildingPrefab;

        [Tooltip("Barracks — tạo bộ binh / cung / đá binh.")]
        [SerializeField] GameObject barracksBuildingPrefab;

        [Tooltip("Defense Tower / tháp canh.")]
        [SerializeField] GameObject defenseTowerBuildingPrefab;

        [Tooltip("Field — ruộng (nếu map có).")]
        [SerializeField] GameObject fieldBuildingPrefab;

        [Header("Unit trên map (tùy chọn — chọn N quân/dân)")]
        [SerializeField] GameObject workerUnitPrefab;
        [SerializeField] GameObject warriorUnitPrefab;
        [SerializeField] GameObject archerUnitPrefab;
        [SerializeField] GameObject rockWarriorUnitPrefab;

        /// <summary>
        /// Mục tiêu: Lấy prefab nhà theo loại voice/AI.
        /// Cách hoạt động: Map VoiceCommandBuildingTarget → field SerializeField tương ứng.
        /// </summary>
        public bool TryGetBuildingPrefab(VoiceCommandBuildingTarget target, out GameObject prefab)
        {
            prefab = target switch
            {
                VoiceCommandBuildingTarget.CivilCentral => civilCentralBuildingPrefab,
                VoiceCommandBuildingTarget.StoreHouse => storeHouseBuildingPrefab,
                VoiceCommandBuildingTarget.Corral => corralBuildingPrefab,
                VoiceCommandBuildingTarget.Forge => forgeBuildingPrefab,
                VoiceCommandBuildingTarget.Barracks => barracksBuildingPrefab,
                VoiceCommandBuildingTarget.DefenseTower => defenseTowerBuildingPrefab,
                VoiceCommandBuildingTarget.Field => fieldBuildingPrefab,
                _ => null
            };

            return prefab != null;
        }

        /// <summary>
        /// Mục tiêu: Lấy prefab unit cho lệnh chọn N quân/dân voice.
        /// Cách hoạt động: Map archetype JSON (worker, warrior, …) → field SerializeField.
        /// </summary>
        public bool TryGetUnitPrefab(string archetype, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(archetype))
            {
                return false;
            }

            prefab = archetype.Trim().ToLowerInvariant() switch
            {
                "worker" => workerUnitPrefab,
                "warrior" => warriorUnitPrefab,
                "archer" => archerUnitPrefab,
                "rockwarrior" => rockWarriorUnitPrefab,
                _ => null
            };

            return prefab != null;
        }
    }
}
