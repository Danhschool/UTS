using GameDevTV.RTS.AI;

namespace GameDevTV.RTS.Game.Pregame
{
    /// <summary>
    /// SRP: Lưu lựa chọn từ menu / màn setup trước khi vào Loading → gameplay.
    /// </summary>
    public static class PregameSessionState
    {
        public const string DefaultGameplayScene = "Game 1";

        public static PregamePlayMode PlayMode { get; private set; } = PregamePlayMode.SinglePlayer;
        public static int SelectedMapIndex { get; private set; }
        public static AIDifficultyLevel SelectedDifficulty { get; private set; } = AIDifficultyLevel.Medium;
        public static string SelectedGameplayScene { get; private set; } = DefaultGameplayScene;

        /// <summary>
        /// Mục tiêu: Ghi nhận chế độ chơi khi chuyển từ MainMenu sang SSScene.
        /// Cách hoạt động: Lưu enum PlayMode để setup UI bật panel SP hoặc MP.
        /// </summary>
        public static void SetPlayMode(PregamePlayMode mode)
        {
            PlayMode = mode;
        }

        /// <summary>
        /// Mục tiêu: Lưu độ khó AI khi user chọn trên SSScene (trước khi bấm Start).
        /// Cách hoạt động: Gán SelectedDifficulty từ UI exclusive select.
        /// </summary>
        public static void SetSelectedDifficulty(AIDifficultyLevel difficulty)
        {
            SelectedDifficulty = difficulty;
        }

        /// <summary>
        /// Mục tiêu: Lưu map + độ khó trước khi bấm Bắt đầu.
        /// Cách hoạt động: Gán index map, enum difficulty và tên scene gameplay.
        /// </summary>
        public static void ConfigureSinglePlayer(int mapIndex, AIDifficultyLevel difficulty, string gameplayScene)
        {
            SelectedMapIndex = mapIndex < 0 ? 0 : mapIndex;
            SelectedDifficulty = difficulty;
            SelectedGameplayScene = string.IsNullOrWhiteSpace(gameplayScene)
                ? DefaultGameplayScene
                : gameplayScene;
        }

        public static void ConfigureMultiplayer(int mapIndex, string gameplayScene)
        {
            SelectedMapIndex = mapIndex < 0 ? 0 : mapIndex;
            SelectedGameplayScene = string.IsNullOrWhiteSpace(gameplayScene)
                ? DefaultGameplayScene
                : gameplayScene;
        }

        public static void ResetToDefaults()
        {
            PlayMode = PregamePlayMode.SinglePlayer;
            SelectedMapIndex = 0;
            SelectedDifficulty = AIDifficultyLevel.Medium;
            SelectedGameplayScene = DefaultGameplayScene;
        }
    }
}
