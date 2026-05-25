using GameDevTV.RTS.Commands;

using GameDevTV.RTS.Units;

using Mirror;

using ProjectRTS.Netplay;

using UnityEngine;

using UnityEngine.InputSystem.LowLevel;



namespace GameDevTV.RTS.Netplay

{

    /// <summary>

    /// SRP: Client relay lệnh unit UTS qua Command thay vì gọi Handle trực tiếp khi không phải host.

    /// </summary>

    public static class RtsUtsClientCommandRelay

    {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

        static void RegisterBridge()

        {

            PlayerInputNetworkBridge.IsMultiplayerClient = () =>

                NetworkClient.active && NetworkClient.isConnected && !NetworkServer.active;



            PlayerInputNetworkBridge.TryRelayUnitCommand = TryRelay;

        }



        static bool TryRelay(

            AbstractUnit unit,

            RaycastHit hit,

            BaseCommand command,

            MouseButton mouseButton,

            int unitIndex)

        {

            if (unit == null || command == null)

            {

                return false;

            }



            if (!unit.TryGetComponent(out NetworkIdentity identity))

            {

                return false;

            }



            if (!unit.TryGetComponent(out RtsUtsNetworkEntity networkEntity)

                || !networkEntity.IsCommandableByLocalHuman)

            {

                return false;

            }



            RtsUtsPlayerCommands commands = NetworkClient.localPlayer != null

                ? NetworkClient.localPlayer.GetComponent<RtsUtsPlayerCommands>()

                : null;



            if (commands == null)

            {

                return false;

            }



            if (command is MoveCommand)

            {

                commands.RequestUtsMove(identity.netId, hit.point, unitIndex);

                return true;

            }



            if (command is GatherCommand)

            {

                commands.RequestUtsGather(identity.netId, hit.point, unitIndex);

                return true;

            }



            return false;

        }

    }

}


