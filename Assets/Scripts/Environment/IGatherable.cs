namespace GameDevTV.RTS.Environment
{
    public interface IGatherable
    {
        public SupplySO Supply { get; }
        public int Amount { get; }

        public void BeginGather();
        public int EndGather(int bonusPerGather = 0);
        public void AbortGather();
    }
}
