using System;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Giữ tên script trên scene/prefab cũ; logic nằm ở <see cref="FactionVisibilityUpdater"/>.
    /// </summary>
    [Obsolete("Dùng FactionVisibilityUpdater. Class này giữ reference scene Game 1.")]
    public class FogVisibilityManager : FactionVisibilityUpdater
    {
    }
}
