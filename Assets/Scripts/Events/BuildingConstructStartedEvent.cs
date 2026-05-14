using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    /// <summary>
    /// Phát khi <see cref="GameDevTV.RTS.Behavior.BuildBuildingAction"/> bắt đầu (nhà được spawn / tiếp tục xây) — dùng để gỡ ghost đặt chỗ trên UI.
    /// </summary>
    public struct BuildingConstructStartedEvent : IEvent
    {
        public Owner Owner { get; private set; }

        public BuildingConstructStartedEvent(Owner owner)
        {
            Owner = owner;
        }
    }
}
