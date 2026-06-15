using GameDevTV.RTS.Game;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Đăng ký hook MP từ Assembly-CSharp sang ProjectRTS.Netplay (tránh reference vòng asmdef).
    /// </summary>
    static class RtsNetworkSceneLoadHooksRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsNetworkSceneLoadHooks.BeginNetworkGameplayLoad = GameplaySceneLoader.BeginNetworkGameplayLoad;
            RtsServerGameplayNotifier.OnPlayerDisconnectedForfeit = HandlePlayerDisconnectedForfeit;
            RtsServerGameplayNotifier.OnClientDisconnectedCleanup = HandleClientDisconnectedCleanup;
        }

        static void HandlePlayerDisconnectedForfeit(int teamSlot)
        {
            Owner disconnectedOwner = OwnerTeamMapping.FromTeamIndex(teamSlot);
            GameMatchOverlayStateSync.RequestMatchEndFromPlayerDisconnect(disconnectedOwner);
        }

        static void HandleClientDisconnectedCleanup()
        {
            RtsUtsServerEntityFactory.ResetMatchRegistration();
        }
    }
}
