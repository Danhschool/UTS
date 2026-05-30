using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Events
{
    /// <summary>
    /// Lệnh đang chờ xác nhận trên map (ghost / placement). Command = null khi hủy hoặc hoàn tất.
    /// </summary>
    public struct ActiveCommandChangedEvent : IEvent
    {
        public BaseCommand Command { get; }

        public ActiveCommandChangedEvent(BaseCommand command)
        {
            Command = command;
        }
    }
}
