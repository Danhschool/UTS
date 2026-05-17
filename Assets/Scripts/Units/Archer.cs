using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class Archer : BaseMilitaryUnit, IProjectileAttacker
    {
        [SerializeField] private HomingArrowFlight arrowFlight;
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float launchCooldownSeconds = 0.35f;

        private float lastLaunchTime = float.NegativeInfinity;

        protected override void Awake()
        {
            base.Awake();
            ResolveArrowFlight();
        }

        protected override void Start()
        {
            base.Start();
            ValidateArcherSetup();
        }

        public void LaunchProjectile(IDamageable target)
        {
            if (target == null || target.Transform == null || unitSO?.AttackConfig == null)
            {
                return;
            }

            LaunchAtTarget(target.Transform.gameObject);
        }

        public void OnFireArrow()
        {
            if (unitSO?.AttackConfig == null)
            {
                return;
            }

            if (TryResolveTargetGameObject(out GameObject target))
            {
                LaunchAtTarget(target);
                return;
            }

            Vector3 fallback = GetFireWorldPosition() + transform.forward * unitSO.AttackConfig.AttackRange;
            IDamageable near = FindDamageableNear(fallback);
            if (near != null)
            {
                LaunchAtTarget(near.Transform.gameObject);
            }
        }

        public void LaunchAtTarget(GameObject target)
        {
            if (target == null || unitSO?.AttackConfig == null)
            {
                return;
            }

            if (!TryBeginLaunch())
            {
                return;
            }

            if (arrowFlight == null)
            {
                ApplyDamageFromTarget(target);
                return;
            }

            arrowFlight.Launch(target);
        }

        private void HandleArrowTargetReached(GameObject target)
        {
            ApplyDamageFromTarget(target);
        }

        private void ApplyDamageFromTarget(GameObject target)
        {
            if (target == null || unitSO?.AttackConfig == null)
            {
                return;
            }

            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.Transform == null || damageable.Owner == Owner)
            {
                return;
            }

            damageable.TakeDamage(unitSO.AttackConfig.Damage);
        }

        private bool TryBeginLaunch()
        {
            if (Time.time < lastLaunchTime + launchCooldownSeconds)
            {
                return false;
            }

            lastLaunchTime = Time.time;
            return true;
        }

        private Vector3 GetFireWorldPosition() =>
            firePoint != null ? firePoint.position : transform.position;

        private void ResolveArrowFlight()
        {
            if (arrowFlight == null)
            {
                arrowFlight = GetComponent<HomingArrowFlight>();
            }

            if (arrowFlight == null)
            {
                arrowFlight = gameObject.AddComponent<HomingArrowFlight>();
            }

            if (arrowPrefab != null)
            {
                arrowFlight.BindArrowPrefab(arrowPrefab);
            }

            arrowFlight.BindFirePoint(firePoint);

            arrowFlight.TargetReached -= HandleArrowTargetReached;
            arrowFlight.TargetReached += HandleArrowTargetReached;
        }

        private bool TryResolveTargetGameObject(out GameObject target)
        {
            target = null;

            if (graphAgent.GetVariable("Target", out BlackboardVariable<GameObject> attackTarget)
                && attackTarget.Value != null)
            {
                target = attackTarget.Value;
                return true;
            }

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value != null)
            {
                target = targetVariable.Value;
                return true;
            }

            return false;
        }

        private void ValidateArcherSetup()
        {
            if (unitSO?.AttackConfig == null)
            {
                Debug.LogError($"Archer {name} has no AttackConfig on UnitSO.");
                return;
            }

            if (!unitSO.AttackConfig.HasProjectileAttacks)
            {
                Debug.LogWarning($"Archer {name}: bật HasProjectileAttacks trên Attack Config.");
            }

            if (arrowPrefab == null && arrowFlight != null)
            {
                Debug.LogWarning($"Archer {name}: gán Arrow Prefab (vd. Assets/Models/Units/Projectiles/Arrow.prefab).");
            }
        }

        private IDamageable FindDamageableNear(Vector3 position)
        {
            if (unitSO?.AttackConfig == null)
            {
                return null;
            }

            Collider[] hits = Physics.OverlapSphere(position, 1.5f, unitSO.AttackConfig.DamageableLayers);
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable candidate = hits[i].GetComponentInParent<IDamageable>();
                if (candidate != null
                    && candidate.Owner != Owner
                    && candidate.Transform != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        protected override void OnDestroy()
        {
            if (arrowFlight != null)
            {
                arrowFlight.TargetReached -= HandleArrowTargetReached;
                arrowFlight.CancelFlight();
            }

            base.OnDestroy();
        }
    }
}
