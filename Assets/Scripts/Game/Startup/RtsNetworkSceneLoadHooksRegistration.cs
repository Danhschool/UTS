using GameDevTV.RTS.Game.Startup;
using ProjectRTS.Netplay;
using UnityEngine;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Đăng ký hook loading MP từ Assembly-CSharp sang ProjectRTS.Netplay.
    /// </summary>
    static class RtsNetworkSceneLoadHooksRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            RtsNetworkSceneLoadHooks.BeginNetworkGameplayLoad = GameplaySceneLoader.BeginNetworkGameplayLoad;
        }
    }
}
