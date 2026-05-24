using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Commands Mirror cho unit/building UTS (game assembly, gắn cùng player prefab với RtsGameCommander).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsUtsPlayerCommands : NetworkBehaviour
    {
        [Command]
        void CmdUtsMoveUnit(uint netId, Vector3 destination, int formationIndex)
        {
            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            {
                return;
            }

            if (!ni.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(connectionToClient.connectionId))
            {
                return;
            }

            if (!ni.TryGetComponent(out AbstractUnit unit))
            {
                return;
            }

            MoveCommand moveCommand = ScriptableObject.CreateInstance<MoveCommand>();
            var context = new CommandContext(
                networkEntity.UtsOwner,
                unit,
                new RaycastHit { point = destination },
                formationIndex);
            if (moveCommand.CanHandle(context))
            {
                moveCommand.Handle(context);
            }

            Destroy(moveCommand);
        }

        /// <summary>
        /// Mục tiêu: Client gửi lệnh di chuyển unit UTS lên server.
        /// Cách hoạt động: Chỉ local player; Mirror Command tới CmdUtsMoveUnit.
        /// </summary>
        public void RequestUtsMove(uint netId, Vector3 worldPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsMoveUnit(netId, worldPoint, formationIndex);
        }
    }
}
