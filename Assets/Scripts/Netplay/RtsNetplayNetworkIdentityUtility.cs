using System.Reflection;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Refresh Mirror NetworkIdentity behaviour list sau khi gắn NetworkBehaviour runtime.
    /// </summary>
    public static class RtsNetplayNetworkIdentityUtility
    {
        static readonly MethodInfo s_initializeNetworkBehaviours = typeof(NetworkIdentity).GetMethod(
            "InitializeNetworkBehaviours",
            BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Mục tiêu: Server/client cùng danh sách NetworkBehaviour trước spawn/deserialize.
        /// Cách hoạt động: Gọi internal InitializeNetworkBehaviours qua reflection sau AddComponent.
        /// </summary>
        public static void RefreshBehaviours(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out NetworkIdentity identity))
            {
                return;
            }

            s_initializeNetworkBehaviours?.Invoke(identity, null);
        }
    }
}
