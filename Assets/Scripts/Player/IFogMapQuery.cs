using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// ISP: Truy vấn explored/vision từ RT fog của một human faction.
    /// </summary>
    public interface IFogMapQuery
    {
        Owner FactionOwner { get; }
        bool IsFogReady { get; }
        bool IsWorldExplored(Vector3 worldPosition);
        bool IsWorldVisible(Vector3 worldPosition);
    }
}
