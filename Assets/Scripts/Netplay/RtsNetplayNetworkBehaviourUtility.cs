using Mirror;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Kiểm tra NetworkBehaviour đã spawn trước khi đọc isServer / ghi SyncVar.
    /// </summary>
    public static class RtsNetplayNetworkBehaviourUtility
    {
        /// <summary>
        /// Mục tiêu: Tránh NullRef khi factory gắn sync component trước NetworkServer.Spawn.
        /// Cách hoạt động: netIdentity tồn tại và netId đã được cấp phát.
        /// </summary>
        public static bool IsSpawned(NetworkBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }

            NetworkIdentity identity = behaviour.netIdentity;
            return identity != null && identity.netId != 0;
        }

        /// <summary>
        /// Mục tiêu: Chỉ server đã spawn entity mới được push SyncVar.
        /// </summary>
        public static bool CanPushServerState(NetworkBehaviour behaviour) =>
            NetworkServer.active && IsSpawned(behaviour);
    }
}
