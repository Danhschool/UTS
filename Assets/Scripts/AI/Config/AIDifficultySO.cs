using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Tham số độ khó AI — một asset cho mỗi chế độ (Dễ / Trung bình / Khó).
    /// Planner đọc overlay từ SO này; không hardcode ngưỡng trong manager.
    /// </summary>
    [CreateAssetMenu(menuName = "RTS/AI/Difficulty", fileName = "AIDifficulty")]
    public sealed class AIDifficultySO : ScriptableObject
    {
        [SerializeField] private AIDifficultyLevel level = AIDifficultyLevel.Medium;
        [SerializeField] private string displayName = "Trung bình";

        [Header("Nhịp quyết định")]
        [SerializeField] private float tickInterval = 0.65f;
        [Tooltip("Trễ phản ứng (giây) — dùng cho milestone sau; lưu trên SO để menu/GameSetup.")]
        [SerializeField] private float reactionDelay = 1f;

        [Header("Chiến thuật")]
        [SerializeField] private float attackPowerThreshold = 1.2f;
        [SerializeField] private float retreatHealthRatio = 0.35f;
        [SerializeField] private float defenseRadius = 42f;
        [SerializeField] private int minArmyBeforeAttack = 6;
        [SerializeField] private bool enableAttack = true;

        [Header("Economy")]
        [SerializeField] private int targetWorkerCountMin = 2;
        [SerializeField] private int targetWorkerCountMax = 10;
        [Range(0f, 1f)]
        [SerializeField] private float expandAggression = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float buildOrderStrictness = 0.7f;
        [Range(0.5f, 1f)]
        [SerializeField] private float stoneWoodFoodIncomeMultiplier = 1f;

        [Header("Scout / thông tin")]
        [SerializeField] private float scoutInterval = 25f;
        [SerializeField] private bool useFairFogMemory = true;

        [Header("Production")]
        [SerializeField] private bool useUnitCounter;

        [Header("Fair play")]
        [SerializeField] private bool allowResourceCheat;

        public AIDifficultyLevel Level => level;
        public string DisplayName => displayName;
        public float TickInterval => tickInterval;
        public float ReactionDelay => reactionDelay;
        public float AttackPowerThreshold => attackPowerThreshold;
        public float RetreatHealthRatio => retreatHealthRatio;
        public float DefenseRadius => defenseRadius;
        public int MinArmyBeforeAttack => minArmyBeforeAttack;
        public bool EnableAttack => enableAttack;
        public int TargetWorkerCountMin => targetWorkerCountMin;
        public int TargetWorkerCountMax => targetWorkerCountMax;
        public float ExpandAggression => expandAggression;
        public float BuildOrderStrictness => buildOrderStrictness;
        public float StoneWoodFoodIncomeMultiplier => stoneWoodFoodIncomeMultiplier;
        public float ScoutInterval => scoutInterval;
        public bool UseFairFogMemory => useFairFogMemory;
        public bool UseUnitCounter => useUnitCounter;
        public bool AllowResourceCheat => allowResourceCheat;

        /// <summary>
        /// Mục tiêu: Gán preset Dễ theo bảng plan M6.
        /// Cách hoạt động: Set từng field serialized rồi đánh dấu dirty (Editor).
        /// </summary>
        public void ApplyEasyPreset()
        {
            level = AIDifficultyLevel.Easy;
            displayName = "Dễ";
            tickInterval = 1.5f;
            reactionDelay = 2f;
            attackPowerThreshold = 1.8f;
            retreatHealthRatio = 0.55f;
            defenseRadius = 55f;
            minArmyBeforeAttack = 8;
            enableAttack = true;
            targetWorkerCountMin = 2;
            targetWorkerCountMax = 6;
            expandAggression = 0.2f;
            buildOrderStrictness = 0.4f;
            stoneWoodFoodIncomeMultiplier = 0.9f;
            scoutInterval = 45f;
            useFairFogMemory = true;
            useUnitCounter = false;
            allowResourceCheat = false;
        }

        /// <summary>
        /// Mục tiêu: Gán preset Trung bình (baseline playtest).
        /// Cách hoạt động: Giá trị cân bằng theo plan M6.
        /// </summary>
        public void ApplyMediumPreset()
        {
            level = AIDifficultyLevel.Medium;
            displayName = "Trung bình";
            tickInterval = 1.05f;
            reactionDelay = 1f;
            attackPowerThreshold = 1.2f;
            retreatHealthRatio = 0.35f;
            defenseRadius = 42f;
            minArmyBeforeAttack = 6;
            enableAttack = true;
            targetWorkerCountMin = 2;
            targetWorkerCountMax = 10;
            expandAggression = 0.5f;
            buildOrderStrictness = 0.7f;
            stoneWoodFoodIncomeMultiplier = 1f;
            scoutInterval = 25f;
            useFairFogMemory = true;
            useUnitCounter = false;
            allowResourceCheat = false;
        }

        /// <summary>
        /// Mục tiêu: Gán preset Khó.
        /// Cách hoạt động: Tick nhanh, scout dày, counter unit khi bật useUnitCounter.
        /// </summary>
        public void ApplyHardPreset()
        {
            level = AIDifficultyLevel.Hard;
            displayName = "Khó";
            tickInterval = 0.55f;
            reactionDelay = 0.4f;
            attackPowerThreshold = 0.85f;
            retreatHealthRatio = 0.22f;
            defenseRadius = 36f;
            minArmyBeforeAttack = 4;
            enableAttack = true;
            targetWorkerCountMin = 3;
            targetWorkerCountMax = 14;
            expandAggression = 0.75f;
            buildOrderStrictness = 0.95f;
            stoneWoodFoodIncomeMultiplier = 1f;
            scoutInterval = 15f;
            useFairFogMemory = true;
            useUnitCounter = true;
            allowResourceCheat = false;
        }
    }
}
