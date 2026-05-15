using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Unit", menuName = "Units/Unit")]
    public class UnitSO : AbstractUnitSO
    {
        [field: SerializeField] public AttackConfigSO AttackConfig { get; private set; }
        [field: SerializeField] public TransportConfigSO TransportConfig { get; private set; }
        [field: SerializeField] public UnitDeathConfigSO DeathConfig { get; private set; }

        [field: SerializeField] public float MoveSpeed { get; private set; } = 3.5f;
        [field: SerializeField, Tooltip("Nhân với BaseGatherTime trên SupplySO (< 1 = khai thác nhanh hơn).")]
        public float GatherTimeMultiplier { get; private set; } = 1f;
        [field: SerializeField, Tooltip("Cộng vào AmountPerGather mỗi chu kỳ khai thác.")]
        public int GatherAmountBonus { get; private set; }

        public override object Clone()
        {
            UnitSO copy = base.Clone() as UnitSO;

            copy.AttackConfig = AttackConfig == null ? null : Instantiate(AttackConfig);
            copy.TransportConfig = TransportConfig == null ? null : Instantiate(TransportConfig);
            copy.DeathConfig = DeathConfig == null ? null : Instantiate(DeathConfig);
            copy.SightConfig = SightConfig == null ? null : Instantiate(SightConfig);

            return copy;
        }
    }
}