using System;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// SRP: Quét binding, gọi handler đã đăng ký. Không phụ thuộc MonoBehaviour.
    /// </summary>
    public sealed class HotkeyService
    {
        readonly IReadOnlyList<HotkeyBindingEntry> bindings;
        readonly IHotkeyInputSource inputSource;
        readonly IHotkeyGate gate;
        readonly Func<Owner> resolveEventOwner;
        readonly Dictionary<HotkeyId, List<IHotkeyHandler>> handlers = new(16);

        public HotkeyService(
            IReadOnlyList<HotkeyBindingEntry> bindings,
            IHotkeyInputSource inputSource,
            IHotkeyGate gate,
            Func<Owner> resolveEventOwner = null)
        {
            this.bindings = bindings ?? HotkeyDefaults.CreateBindings();
            this.inputSource = inputSource;
            this.gate = gate;
            this.resolveEventOwner = resolveEventOwner ?? LocalHumanOwnerAccess.GetLocalOwnerOrDefault;
        }

        /// <summary>
        /// Mục tiêu: Gắn logic xử lý cho một HotkeyId.
        /// Cách hoạt động: Lưu handler vào dictionary; cho phép nhiều handler cùng Id.
        /// </summary>
        public void Register(IHotkeyHandler handler)
        {
            if (handler == null || handler.Id == HotkeyId.None)
            {
                return;
            }

            if (!handlers.TryGetValue(handler.Id, out List<IHotkeyHandler> list))
            {
                list = new List<IHotkeyHandler>(2);
                handlers[handler.Id] = list;
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void Unregister(IHotkeyHandler handler)
        {
            if (handler == null || !handlers.TryGetValue(handler.Id, out List<IHotkeyHandler> list))
            {
                return;
            }

            list.Remove(handler);
        }

        /// <summary>
        /// Mục tiêu: Quét phím mỗi frame và dispatch handler tương ứng.
        /// Cách hoạt động: Với mỗi binding, nếu chord khớp và không bị gate chặn thì gọi handler + raise event.
        /// </summary>
        public void Tick()
        {
            inputSource.Refresh();

            if (!inputSource.IsAvailable)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                HotkeyBindingEntry entry = bindings[i];
                if (entry.id == HotkeyId.None || entry.chords == null || entry.chords.Length == 0)
                {
                    continue;
                }

                if (!TryGetMatchedChord(entry.chords, out HotkeyChord matched))
                {
                    continue;
                }

                var context = new HotkeyContext(entry.id, matched);
                if (gate != null && gate.IsBlocked(in context))
                {
                    continue;
                }

                DispatchHandlers(in context);
                Bus<HotkeyTriggeredEvent>.Raise(
                    resolveEventOwner(),
                    new HotkeyTriggeredEvent(entry.id, matched));
            }
        }

        bool TryGetMatchedChord(HotkeyChord[] chords, out HotkeyChord matched)
        {
            for (int i = 0; i < chords.Length; i++)
            {
                if (inputSource.WasChordPressedThisFrame(chords[i]))
                {
                    matched = chords[i];
                    return true;
                }
            }

            matched = default;
            return false;
        }

        void DispatchHandlers(in HotkeyContext context)
        {
            if (!handlers.TryGetValue(context.Id, out List<IHotkeyHandler> list))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                list[i].Execute(in context);
            }
        }
    }
}
