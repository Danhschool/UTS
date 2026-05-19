namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Passive building behaviours toggled with <see cref="BaseBuilding"/> enable/disable.
    /// </summary>
    public interface IBuildingPassiveEffect
    {
        void SetEffectActive(bool isActive);
    }
}
