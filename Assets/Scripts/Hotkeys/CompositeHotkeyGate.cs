namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// ISP: Kết hợp nhiều gate nhỏ (UI focus, MP local owner…).
    /// </summary>
    public sealed class CompositeHotkeyGate : IHotkeyGate
    {
        readonly IHotkeyGate[] gates;

        public CompositeHotkeyGate(params IHotkeyGate[] gates)
        {
            this.gates = gates ?? System.Array.Empty<IHotkeyGate>();
        }

        public bool IsBlocked(in HotkeyContext context)
        {
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i] != null && gates[i].IsBlocked(in context))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
