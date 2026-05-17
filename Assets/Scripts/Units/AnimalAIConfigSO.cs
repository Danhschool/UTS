using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(menuName = "RTS/Animal AI Config", fileName = "AnimalAIConfig")]
    public class AnimalAIConfigSO : ScriptableObject
    {
        [Header("Roam")]
        [SerializeField] private float roamRadius = 25f;
        [SerializeField] private float roamMinDistance = 8f;

        [Header("Combat")]
        [SerializeField] private float attackSensorRange = 12f;
        [SerializeField] private float fleeHealthFraction = 0.35f;
        [SerializeField] private float fleeDistance = 22f;

        [Header("Eat (animation only)")]
        [SerializeField] private float eatDurationSeconds = 3f;
        [Range(0f, 1f)]
        [SerializeField] private float eatChancePerTick = 0.25f;

        [Header("Corpse food")]
        [SerializeField] private SupplySO corpseFoodSupply;
        [SerializeField] private int corpseFoodAmount = 300;
        [SerializeField] private GameObject corpseSupplyPrefab;

        public float RoamRadius => roamRadius;
        public float RoamMinDistance => roamMinDistance;
        public float AttackSensorRange => attackSensorRange;
        public float FleeHealthFraction => fleeHealthFraction;
        public float FleeDistance => fleeDistance;
        public float EatDurationSeconds => eatDurationSeconds;
        public float EatChancePerTick => eatChancePerTick;
        public SupplySO CorpseFoodSupply => corpseFoodSupply;
        public int CorpseFoodAmount => corpseFoodAmount;
        public GameObject CorpseSupplyPrefab => corpseSupplyPrefab;
    }
}
