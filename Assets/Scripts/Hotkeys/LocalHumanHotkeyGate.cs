using GameDevTV.RTS.Player;
using Mirror;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Chặn hotkey khi MP chưa gán LocalOwner (P2 vào game trễ, lobby chưa sync…).
    /// Offline: không chặn (LocalHumanOwnerService tự bootstrap P1).
    /// </summary>
    public sealed class LocalHumanHotkeyGate : IHotkeyGate
    {
        public bool IsBlocked(in HotkeyContext context)
        {
            if (!NetworkClient.active)
            {
                return false;
            }

            if (!NetworkClient.isConnected)
            {
                return true;
            }

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return true;
            }

            return false;
        }
    }
}
