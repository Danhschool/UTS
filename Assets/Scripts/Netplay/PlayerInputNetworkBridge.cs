using System;
using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// DIP: PlayerInput (game) gọi relay lệnh MP qua handler đăng ký (không phụ thuộc Mirror trực tiếp).
    /// </summary>
    public static class PlayerInputNetworkBridge
    {
        public static Func<bool> IsMultiplayerClient;

        public static Func<AbstractUnit, RaycastHit, BaseCommand, MouseButton, int, bool> TryRelayUnitCommand;

        public static Func<AbstractUnit, bool> TryRelayUnitStop;

        public static Func<BaseCommand, IReadOnlyList<AbstractCommandable>, RaycastHit, bool> TryRelayActivateCommand;

        public static Func<IReadOnlyList<AbstractUnit>, RaycastHit, MoveCommand, bool> TryRelayFormationMove;

        public static bool ShouldRelayCommands =>
            IsMultiplayerClient != null && IsMultiplayerClient.Invoke();
    }
}
