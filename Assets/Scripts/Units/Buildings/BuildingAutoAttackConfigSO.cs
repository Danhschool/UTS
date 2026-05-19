using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Buildings
{
    [CreateAssetMenu(fileName = "Building Auto Attack Config", menuName = "Buildings/Building Auto Attack Config")]
    public class BuildingAutoAttackConfigSO : ScriptableObject
    {
        [field: SerializeField] public AttackConfigSO AttackConfig { get; private set; }
        [field: SerializeField] public bool RequireTargetVisible { get; private set; } = true;
        [field: SerializeField] public GameObject ProjectilePrefab { get; private set; }
    }
}
