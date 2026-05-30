using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Base MonoBehaviour cho handler: kéo vào HotkeySystem hoặc để con của cùng GameObject.
    /// </summary>
    public abstract class HotkeyHandlerBase : MonoBehaviour, IHotkeyHandler
    {
        public abstract HotkeyId Id { get; }
        public abstract void Execute(in HotkeyContext context);
    }
}
