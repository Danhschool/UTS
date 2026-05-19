using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Buildings
{
    [CreateAssetMenu(fileName = "Passive Food Generator Config", menuName = "Buildings/Passive Food Generator Config")]
    public class PassiveFoodGeneratorConfigSO : ScriptableObject
    {
        [field: SerializeField, Min(0.1f)] public float IntervalSeconds { get; private set; } = 10f;
        [field: SerializeField, Min(1)] public int FoodPerTick { get; private set; } = 5;
        [field: SerializeField] public SupplySO FoodSupply { get; private set; }
    }
}
