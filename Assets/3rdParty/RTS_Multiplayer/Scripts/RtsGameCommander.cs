using Mirror;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Kênh lệnh RTS từ client (local player) tới server cho đơn vị thuộc quyền.
    /// Cách hoạt động: Client gọi Command với netId đích; server kiểm tra owner rồi cập nhật đích di chuyển trên RtsUnit.
    /// </summary>
    public class RtsGameCommander : NetworkBehaviour
    {
        [Command]
        public void CmdMoveUnit(uint netId, Vector3 destination)
        {
            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
                return;
            var unit = ni.GetComponent<RtsUnit>();
            if (unit == null || !unit.ServerCanOrder(connectionToClient.connectionId))
                return;
            unit.ServerSetDestination(destination);
        }

        /// <summary>Gọi từ client (input) cho local player.</summary>
        public void RequestMoveUnit(uint netId, Vector3 worldPoint)
        {
            if (!isLocalPlayer)
                return;
            CmdMoveUnit(netId, worldPoint);
        }
    }
}
