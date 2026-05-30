using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

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
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            MoveCommand moveCommand = ScriptableObject.CreateInstance<MoveCommand>();
            CommandContext context = new CommandContext(
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

        [Command]
        void CmdUtsStop(uint netId)
        {
            if (!TryResolveCommandable(netId, out _, out AbstractUnit unit))
            {
                return;
            }

            unit.Stop();
        }

        [Command]
        void CmdUtsGather(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            RaycastHit hit = BuildHitFromWorldPoint(hitPoint);
            foreach (ICommand candidate in AvailableCommandsResolver.GetFlattened(unit))
            {
                if (candidate is not GatherCommand gatherCommand)
                {
                    continue;
                }

                CommandContext context = new CommandContext(
                    networkEntity.UtsOwner,
                    unit,
                    hit,
                    formationIndex,
                    MouseButton.Right);

                if (gatherCommand.CanHandle(context))
                {
                    gatherCommand.Handle(context);
                }

                return;
            }
        }

        bool TryResolveCommandable(
            uint netId,
            out RtsUtsNetworkEntity networkEntity,
            out AbstractUnit unit)
        {
            networkEntity = null;
            unit = null;

            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            {
                return false;
            }

            if (!ni.TryGetComponent(out networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(connectionToClient.connectionId))
            {
                return false;
            }

            return ni.TryGetComponent(out unit);
        }

        static RaycastHit BuildHitFromWorldPoint(Vector3 worldPoint)
        {
            RaycastHit hit = new RaycastHit { point = worldPoint };
            Vector3 origin = worldPoint + Vector3.up * 80f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit physicsHit, 160f))
            {
                hit = physicsHit;
            }

            return hit;
        }

        /// <summary>
        /// Mục tiêu: Client gửi lệnh Stop (phím H) lên server cho unit thuộc local human.
        /// Cách hoạt động: Chỉ local player gọi Command; server chạy Stop() trên unit đã resolve.
        /// </summary>
        public void RequestUtsStop(uint netId)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsStop(netId);
        }

        /// <summary>
        /// Mục tiêu: Client gửi lệnh di chuyển unit UTS lên server.
        /// </summary>
        public void RequestUtsMove(uint netId, Vector3 worldPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsMoveUnit(netId, worldPoint, formationIndex);
        }

        /// <summary>
        /// Mục tiêu: Client P2 gửi lệnh thu thập — server chạy GatherCommand.
        /// </summary>
        public void RequestUtsGather(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsGather(netId, hitPoint, formationIndex);
        }
    }
}
