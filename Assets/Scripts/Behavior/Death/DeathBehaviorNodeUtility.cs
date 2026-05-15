using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Behavior.Death
{
    internal static class DeathBehaviorNodeUtility
    {
        public static bool TryGetDeathController(GameObject self, out UnitDeathController controller)
        {
            controller = null;
            return self != null && self.TryGetComponent(out controller);
        }
    }
}
