using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Buildings
{
    /// <summary>
    /// Automatically attacks the nearest hostile unit in range (owner different from this building).
    /// Spawns a projectile from an elevated fire point and fires straight down toward DamageableSensor.
    /// </summary>
    [RequireComponent(typeof(BaseBuilding))]
    public class BuildingAutoAttack : MonoBehaviour, IBuildingPassiveEffect
    {
        [SerializeField] private BuildingAutoAttackConfigSO config;
        [SerializeField] private BaseBuilding building;
        [Tooltip("Điểm bắn trên đỉnh tháp (Y cao). Để trống thì dùng transform gốc.")]
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private TowerDownwardProjectileFlight projectileFlight;

        private float nextAttackTime;

        private void Awake()
        {
            if (building == null)
            {
                building = GetComponent<BaseBuilding>();
            }

            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            ResolveProjectileFlight();
            enabled = false;
        }

        public void SetEffectActive(bool isActive)
        {
            enabled = isActive;
        }

        private void OnDestroy()
        {
            if (projectileFlight != null)
            {
                projectileFlight.TargetReached -= HandleProjectileTargetReached;
                projectileFlight.CancelFlight();
            }
        }

        private void Update()
        {
            if (RtsNetplaySession.IsNetworkMatch && !NetworkServer.active)
            {
                return;
            }

            if (config == null || config.AttackConfig == null)
            {
                return;
            }

            if (!BuildingEffectUtility.IsOperational(building))
            {
                return;
            }

            if (Time.time < nextAttackTime)
            {
                return;
            }

            AttackConfigSO attackConfig = config.AttackConfig;
            if (!HostileTargetLocator.TryFindClosestHostile(
                    attackOrigin.position,
                    attackConfig.AttackRange,
                    building.Owner,
                    attackConfig.DamageableLayers,
                    config.RequireTargetVisible,
                    out IDamageable target))
            {
                return;
            }

            nextAttackTime = Time.time + attackConfig.AttackDelay;

            if (UsesProjectileAttack())
            {
                if (DamageableSensorAimUtility.TryGetSensorAimPosition(target, out Vector3 aimPosition))
                {
                    projectileFlight.LaunchAt(aimPosition, target.Transform.gameObject);
                }
            }
            else
            {
                target.TakeDamage(attackConfig.Damage, building);
            }
        }

        private bool UsesProjectileAttack() =>
            config.ProjectilePrefab != null && projectileFlight != null;

        /// <summary>
        /// Mục tiêu: Gây damage khi projectile chạm mục tiêu.
        /// Cách hoạt động: Lấy IDamageable trên target; bỏ qua cùng Owner; gọi TakeDamage với AttackConfig.Damage.
        /// </summary>
        private void HandleProjectileTargetReached(GameObject target)
        {
            if (!RtsNetplaySession.ShouldApplyCombatDamage)
            {
                return;
            }

            if (target == null || config?.AttackConfig == null)
            {
                return;
            }

            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null
                || damageable.Transform == null
                || damageable.Owner == building.Owner
                || damageable.CurrentHealth <= 0)
            {
                return;
            }

            damageable.TakeDamage(config.AttackConfig.Damage, building);
        }

        private void ResolveProjectileFlight()
        {
            if (projectileFlight == null)
            {
                projectileFlight = GetComponent<TowerDownwardProjectileFlight>();
            }

            if (projectileFlight == null)
            {
                projectileFlight = gameObject.AddComponent<TowerDownwardProjectileFlight>();
            }

            if (config != null && config.ProjectilePrefab != null)
            {
                projectileFlight.BindProjectilePrefab(config.ProjectilePrefab);
            }

            projectileFlight.BindFirePoint(attackOrigin);
            projectileFlight.TargetReached -= HandleProjectileTargetReached;
            projectileFlight.TargetReached += HandleProjectileTargetReached;
        }
    }
}
