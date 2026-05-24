using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// ISP: Nguồn sự thật cho human player đang điều khiển trên client hiện tại.
    /// </summary>
    public interface ILocalHumanOwner
    {
        Owner LocalOwner { get; }
        bool IsInitialized { get; }
        bool IsLocalOwner(Owner owner);
    }
}
