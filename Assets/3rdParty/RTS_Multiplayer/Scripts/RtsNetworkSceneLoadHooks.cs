using System;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Hook tĩnh để Assembly-CSharp đăng ký luồng Loading scene — tránh reference vòng.
    /// </summary>
    public static class RtsNetworkSceneLoadHooks
    {
        /// <summary>Gọi trước khi host/client chuyển sang Loading scene (target = game scene).</summary>
        public static Action<string> BeginNetworkGameplayLoad;
    }
}
