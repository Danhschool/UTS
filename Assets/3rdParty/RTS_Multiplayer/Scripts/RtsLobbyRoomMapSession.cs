namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Map lobby host chọn trước khi RtsLobbyPlayer spawn xong (buffer server-side).
    /// </summary>
    public static class RtsLobbyRoomMapSession
    {
        public static int PendingIndex { get; private set; }
        public static string PendingSceneName { get; private set; } = PregameGameplaySceneFallback.DefaultScene;

        /// <summary>
        /// Mục tiêu: Ghi map host chọn để áp khi spawn player hoặc bắt đầu trận.
        /// Cách hoạt động: Chuẩn hóa tên scene và lưu index + scene name.
        /// </summary>
        public static void SetPending(int index, string sceneName)
        {
            PendingIndex = index < 0 ? 0 : index;
            PendingSceneName = RtsNetSceneUtility.NormalizeSceneName(sceneName);
        }
    }
}
