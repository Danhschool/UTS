using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Tạo bản instance offline từ prefab (gỡ Mirror nếu prefab từng wire MP).
    /// </summary>
    public static class PvAiOfflineEntityFactory
    {
        /// <summary>
        /// Mục tiêu: Spawn unit/building PvE.
        /// Cách hoạt động: Instantiate → gỡ NetworkIdentity/NetworkBehaviour → gán Owner + fog vision.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Owner owner)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            StripNetworkComponents(instance);

            if (instance.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.SyncOwnerAndFogVision(owner);
            }

            return instance;
        }

        /// <summary>
        /// Mục tiêu: Prefab có thể còn component Mirror từ lần tích hợp MP trước.
        /// Cách hoạt động: Destroy mọi NetworkBehaviour rồi NetworkIdentity trên root.
        /// </summary>
        static void StripNetworkComponents(GameObject root)
        {
            NetworkBehaviour[] behaviours = root.GetComponents<NetworkBehaviour>();
            for (int i = behaviours.Length - 1; i >= 0; i--)
            {
                if (behaviours[i] != null)
                {
                    Object.DestroyImmediate(behaviours[i]);
                }
            }

            if (root.TryGetComponent(out NetworkIdentity identity))
            {
                Object.DestroyImmediate(identity);
            }
        }
    }
}
