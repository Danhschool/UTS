using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Buildings
{
    /// <summary>
    /// Shared checks for passive building effects (food gen, tower attack, …).
    /// </summary>
    public static class BuildingEffectUtility
    {
        public static bool IsOperational(BaseBuilding building) =>
            building != null
            && building.isActiveAndEnabled
            && building.CurrentHealth > 0;
    }
}
