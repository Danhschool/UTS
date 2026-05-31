using System;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Cổng "gameplay đã mở" — input/audio/UI gameplay chỉ chạy khi unlocked.
    /// Không đụng Time.timeScale.
    /// </summary>
    public static class GameplayStartupGate
    {
        public static event Action Unlocked;

        public static bool IsGameplayUnlocked { get; private set; } = true;

        public static void Lock()
        {
            if (!IsGameplayUnlocked)
            {
                return;
            }

            IsGameplayUnlocked = false;
        }

        public static void Unlock()
        {
            if (IsGameplayUnlocked)
            {
                return;
            }

            IsGameplayUnlocked = true;
            Unlocked?.Invoke();
        }

        public static void ForceUnlock()
        {
            if (IsGameplayUnlocked)
            {
                return;
            }

            IsGameplayUnlocked = true;
            Unlocked?.Invoke();
        }
    }
}
