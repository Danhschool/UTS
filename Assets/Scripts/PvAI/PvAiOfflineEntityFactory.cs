using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Tạo bản instance offline từ prefab MP (không NetworkIdentity trên instance).
    /// </summary>
    public static class PvAiOfflineEntityFactory
    {
        /// <summary>
        /// Mục tiêu: Spawn unit/building PvE từ prefab dùng chung với MP.
        /// Cách hoạt động: Instantiate → gỡ Mirror/network UTS → gán Owner + fog vision.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Owner owner)
        {
            if (prefab == null)
            {
                return null;
            }

            // Awake trên prefab chạy trong Instantiate; sau đó gỡ network rồi mới gán Owner.
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            StripNetworkComponents(instance);

            if (instance.TryGetComponent(out AbstractCommandable commandable))
            {
                commandable.SyncOwnerAndFogVision(owner);
            }

            return instance;
        }

        /// <summary>
        /// Mục tiêu: Gỡ Mirror khỏi instance PvE (prefab dùng chung với MP).
        /// Cách hoạt động: Xóa RtsUtsNetworkEntity trước (RequireComponent NetworkIdentity), rồi transform, rồi identity.
        /// </summary>
        static void StripNetworkComponents(GameObject root)
        {
            if (root.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                Object.DestroyImmediate(networkEntity);
            }

            if (root.TryGetComponent(out NetworkTransformUnreliable networkTransform))
            {
                Object.DestroyImmediate(networkTransform);
            }

            if (root.TryGetComponent(out NetworkIdentity identity))
            {
                Object.DestroyImmediate(identity);
            }
        }
    }
}
