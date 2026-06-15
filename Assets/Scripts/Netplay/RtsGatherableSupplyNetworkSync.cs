using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Gameplay;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server relay lượng tài nguyên mỏ scene xuống client MP (mỏ không NetworkSpawn).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class RtsGatherableSupplyNetworkSync : NetworkBehaviour
    {
        public static RtsGatherableSupplyNetworkSync Instance { get; private set; }

        const float SupplySearchRadius = 6f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RtsGatherableSupplyNetworkSync] Trùng instance — giữ object đầu tiên.");
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void EnsureServerInstance()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (Instance != null)
            {
                return;
            }

            RtsGatherableSupplyNetworkSync existing = Object.FindFirstObjectByType<RtsGatherableSupplyNetworkSync>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            GameplayMapCoreMarker core = Object.FindFirstObjectByType<GameplayMapCoreMarker>(
                FindObjectsInactive.Include);
            GameObject host = core != null ? core.gameObject : new GameObject(nameof(RtsGatherableSupplyNetworkSync));

            if (host.GetComponent<NetworkIdentity>() == null)
            {
                host.AddComponent<NetworkIdentity>();
            }

            if (host.GetComponent<RtsGatherableSupplyNetworkSync>() == null)
            {
                host.AddComponent<RtsGatherableSupplyNetworkSync>();
            }

            if (host.GetComponent<NetworkIdentity>().netId == 0)
            {
                NetworkServer.Spawn(host);
            }
        }

        /// <summary>
        /// Mục tiêu: Server báo client cập nhật/hủy mỏ sau gather.
        /// Cách hoạt động: ClientRpc với world position + amount; client tìm GatherableSupply gần nhất.
        /// </summary>
        public void ServerReportSupplyChanged(Vector3 worldPosition, int amount, bool depleted)
        {
            if (!NetworkServer.active || netIdentity == null || netId == 0)
            {
                return;
            }

            RpcApplySupplyState(worldPosition, amount, depleted);
        }

        [ClientRpc]
        void RpcApplySupplyState(Vector3 worldPosition, int amount, bool depleted)
        {
            if (isServer)
            {
                return;
            }

            if (!TryFindSupplyNear(worldPosition, out GatherableSupply supply))
            {
                return;
            }

            if (depleted || amount <= 0)
            {
                if (supply != null && supply.gameObject != null)
                {
                    Object.Destroy(supply.gameObject);
                }

                return;
            }

            supply.ApplyNetworkAmountSnapshot(amount);
        }

        static bool TryFindSupplyNear(Vector3 worldPosition, out GatherableSupply supply)
        {
            supply = null;
            Collider[] overlaps = Physics.OverlapSphere(
                worldPosition,
                SupplySearchRadius,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null)
                {
                    continue;
                }

                GatherableSupply candidate = collider.GetComponentInParent<GatherableSupply>();
                if (candidate == null)
                {
                    continue;
                }

                float distanceSq = (candidate.transform.position - worldPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                supply = candidate;
            }

            return supply != null;
        }
    }
}
