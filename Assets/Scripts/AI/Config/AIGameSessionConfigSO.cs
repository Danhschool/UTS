using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chọn độ khó mặc định khi vào trận (menu / GameSetup).
    /// </summary>
    [CreateAssetMenu(menuName = "RTS/AI/Game Session Config", fileName = "AIGameSessionConfig")]
    public sealed class AIGameSessionConfigSO : ScriptableObject
    {
        [SerializeField] private AIDifficultyLevel defaultDifficulty = AIDifficultyLevel.Medium;
        [SerializeField] private AIDifficultySO easy;
        [SerializeField] private AIDifficultySO medium;
        [SerializeField] private AIDifficultySO hard;

        public AIDifficultyLevel DefaultDifficulty => defaultDifficulty;

        /// <summary>
        /// Mục tiêu: Lấy asset difficulty theo enum lobby.
        /// Cách hoạt động: Fallback Medium nếu reference trống.
        /// </summary>
        public AIDifficultySO Resolve(AIDifficultyLevel level)
        {
            return level switch
            {
                AIDifficultyLevel.Easy => easy != null ? easy : medium,
                AIDifficultyLevel.Hard => hard != null ? hard : medium,
                _ => medium != null ? medium : easy
            };
        }

        public AIDifficultySO Easy => easy;
        public AIDifficultySO Medium => medium;
        public AIDifficultySO Hard => hard;
    }
}
