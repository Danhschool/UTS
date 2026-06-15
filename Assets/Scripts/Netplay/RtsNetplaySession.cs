using Mirror;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Trạng thái session Mirror cho gameplay UTS (LAN 2 người).
    /// </summary>
    public static class RtsNetplaySession
    {
        public static bool IsNetworkMatch =>
            NetworkClient.active || NetworkServer.active;

        public static bool IsServer => NetworkServer.active;

        public static bool IsPureClient =>
            NetworkClient.active && NetworkClient.isConnected && !NetworkServer.active;

        /// <summary>
        /// Mục tiêu: Chỉ máy server/host mới spawn entity, chạy queue train và Instantiate nhà.
        /// Cách hoạt động: Offline luôn true; MP chỉ khi NetworkServer.active.
        /// </summary>
        public static bool ShouldRunAuthoritativeGameplay =>
            !IsNetworkMatch || NetworkServer.active;

        /// <summary>
        /// Mục tiêu: Chỉ server/host mới được gây sát thương gameplay trong MP.
        /// </summary>
        public static bool ShouldApplyCombatDamage =>
            !IsNetworkMatch || NetworkServer.active;
    }
}
