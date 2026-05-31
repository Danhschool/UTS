namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Chuyển giao audio giữa menu/setup và gameplay.
    /// </summary>
    public static class PregameAudioTransition
    {
        /// <summary>
        /// Mục tiêu: Tắt nhạc menu trước khi vào Loading / scene game.
        /// Cách hoạt động: Gọi AudioAccess.TryStopMusic; gameplay bật nhạc riêng sau khi load xong.
        /// </summary>
        public static void StopMenuMusicForGameplay()
        {
            AudioAccess.TryStopMusic();
        }
    }
}
