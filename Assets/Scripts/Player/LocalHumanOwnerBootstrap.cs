using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Gán <see cref="Owner.Player1"/> khi chơi offline trên scene không có Mirror local player.
    /// Gắn trên Game 1 (hoặc scene single-player); MP lobby dùng <see cref="LocalHumanOwnerMirrorBridge"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LocalHumanOwnerBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var service = LocalHumanOwnerService.EnsureExists();

            if (NetworkClient.active)
                return;

            service.TryBootstrapOfflinePlayer1();
        }
    }
}
