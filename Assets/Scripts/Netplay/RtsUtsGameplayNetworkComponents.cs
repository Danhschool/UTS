using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Gắn và đăng ký Mirror sync components cho entity gameplay spawn runtime.
    /// </summary>
    public static class RtsUtsGameplayNetworkComponents
    {
        /// <summary>
        /// Mục tiêu: Server và client có cùng NetworkBehaviour list trước Mirror spawn/deserialize.
        /// Cách hoạt động: AddComponent thiếu → RefreshBehaviours để Mirror nhận Combat/Building sync.
        /// </summary>
        public static void ApplyToSpawnedInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            EnsureComponents(instance);
            RtsNetplayNetworkIdentityUtility.RefreshBehaviours(instance);
        }

        internal static void EnsureComponents(GameObject instance)
        {
            if (instance.TryGetComponent(out AbstractUnit _))
            {
                EnsureUnitNetworkTransform(instance);
            }
            else if (instance.TryGetComponent(out BaseBuilding _))
            {
                StripBuildingNetworkTransform(instance);
            }

            if (!instance.TryGetComponent(out RtsUtsNetworkCombatSync _))
            {
                instance.AddComponent<RtsUtsNetworkCombatSync>();
            }

            if (instance.TryGetComponent(out BaseBuilding _)
                && !instance.TryGetComponent(out RtsUtsNetworkBuildingSync _))
            {
                instance.AddComponent<RtsUtsNetworkBuildingSync>();
            }
        }

        static void EnsureUnitNetworkTransform(GameObject instance)
        {
            if (!instance.TryGetComponent(out NetworkTransformUnreliable networkTransform))
            {
                networkTransform = instance.AddComponent<NetworkTransformUnreliable>();
            }

            if (instance.TryGetComponent(out AbstractUnit _))
            {
                networkTransform.syncInterval = 0.05f;
            }
        }

        static void StripBuildingNetworkTransform(GameObject instance)
        {
            NetworkTransformUnreliable[] transforms = instance.GetComponents<NetworkTransformUnreliable>();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    Object.Destroy(transforms[i]);
                }
            }
        }
    }
}
