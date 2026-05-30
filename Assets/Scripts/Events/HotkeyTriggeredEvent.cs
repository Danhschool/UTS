using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Hotkeys;

namespace GameDevTV.RTS.Events
{
    public struct HotkeyTriggeredEvent : IEvent
    {
        public HotkeyId Id { get; }
        public HotkeyChord MatchedChord { get; }

        public HotkeyTriggeredEvent(HotkeyId id, HotkeyChord matchedChord)
        {
            Id = id;
            MatchedChord = matchedChord;
        }
    }
}
