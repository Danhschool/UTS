using Mirror;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Đơn vị RTS đồng bộ — server di chuyển tới đích, Mirror replicate transform.
    /// Cách hoạt động: Server cập nhật vị trí mỗi frame; NetworkTransformUnreliable đẩy snapshot xuống client.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class RtsUnit : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 8f;
        [SerializeField] Color team0Color = new Color(0.23f, 0.57f, 1f, 1f);
        [SerializeField] Color team1Color = new Color(1f, 0.36f, 0.36f, 1f);

        [SyncVar]
        int serverOwnerConnectionId = -1;

        [SyncVar(hook = nameof(HookTeamIndex))]
        int teamIndex;

        /// <summary>NetId của player chủ — client so với NetworkClient.localPlayer.netId (không dùng connectionId trên client).</summary>
        [SyncVar]
        uint ownerPlayerNetId;

        Vector3 serverDestination;
        Renderer _cachedRenderer;
        Material _runtimeMaterial;

        public int TeamIndex => teamIndex;

        public bool IsOwnedLocally =>
            isClient &&
            NetworkClient.localPlayer != null &&
            ownerPlayerNetId != 0 &&
            ownerPlayerNetId == NetworkClient.localPlayer.netId;

        void Awake()
        {
            _cachedRenderer = GetComponentInChildren<Renderer>();
            if (_cachedRenderer != null)
            {
                // Clone material at runtime so team color changes don't affect shared asset material.
                _runtimeMaterial = _cachedRenderer.material;
            }
        }

        void Update()
        {
            if (!isServer)
                return;

            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(
                transform.position,
                serverDestination,
                moveSpeed * Time.deltaTime);
            Vector3 delta = transform.position - before;
            if (delta.sqrMagnitude > 0.000001f)
                transform.forward = delta.normalized;
        }

        /// <summary>Mục tiêu: Gán chủ đơn vị trên server. Cách hoạt động: Lưu connectionId cho lệnh Cmd; lưu netId player để client biết unit của mình.</summary>
        public void ServerAssignOwner(int connectionId, int team, uint playerNetId)
        {
            serverOwnerConnectionId = connectionId;
            teamIndex = team;
            ownerPlayerNetId = playerNetId;
            serverDestination = transform.position;
            ApplyTeamVisual(teamIndex);
        }

        public void ServerSetDestination(Vector3 world)
        {
            serverDestination = world;
        }

        public bool ServerCanOrder(int connectionId)
        {
            return connectionId == serverOwnerConnectionId;
        }

        void HookTeamIndex(int oldTeam, int newTeam)
        {
            ApplyTeamVisual(newTeam);
        }

        /// <summary>
        /// Mục tiêu: Hiển thị màu nhân vật theo phe để phân biệt host/client dễ hơn khi test.
        /// Cách hoạt động: Team 0 dùng màu xanh, team 1 dùng màu đỏ; màu được áp vào material runtime của renderer đã cache.
        /// </summary>
        void ApplyTeamVisual(int team)
        {
            if (_runtimeMaterial == null)
                return;
            _runtimeMaterial.color = team == 0 ? team0Color : team1Color;
        }
    }
}
