using System;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// DIP: NetworkManager báo đã vào scene trận; client sync presentation/fog.
    /// </summary>
    public static class RtsMatchSceneClientNotifier
    {
        public static Action OnGameSceneLoaded;

        public static void NotifyGameSceneLoaded() => OnGameSceneLoaded?.Invoke();
    }
}
