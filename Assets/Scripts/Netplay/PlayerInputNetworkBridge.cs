using System;
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

        public static bool ShouldRelayCommands =>
            IsMultiplayerClient != null && IsMultiplayerClient.Invoke();
    }
}
