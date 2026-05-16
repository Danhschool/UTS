using System.Collections;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Unit bắn cung: damage qua <see cref="IProjectileAttacker"/> (Behavior Graph) và/hoặc Animation Event <see cref="OnFireArrow"/>.
    /// </summary>
    public class Archer : BaseMilitaryUnit, IProjectileAttacker
    {
        [SerializeField] private GameObject arrow;
        [Tooltip("Điểm bắn; để trống dùng transform của arrow.")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private float projectileFlightSpeed = 4f;
        [SerializeField] private float arcHeight = 1.5f;
        [Tooltip("Tránh bắn hai lần khi vừa có AttackTargetAction vừa có Animation Event.")]
        [SerializeField] private float launchCooldownSeconds = 0.35f;

        private Transform arrowParent;
        private Vector3 defaultArrowLocalPosition;
        private Quaternion defaultArrowLocalRotation;
        private Coroutine arrowFlightRoutine;
        private float lastLaunchTime = float.NegativeInfinity;

        protected override void Awake()
        {
            base.Awake();
            CacheArrowHolster();
        }

        protected override void Start()
        {
            base.Start();
            ValidateArcherSetup();
        }

        /// <summary>
        /// Mục tiêu: Entry point từ AttackTargetAction — đảm bảo có damage kể cả khi Animation Event lỗi.
        /// Cách hoạt động: Nhận IDamageable đích, bay mũi tên (nếu có), ApplyDamage khi tới.
        /// </summary>
        public void LaunchProjectile(IDamageable target)
        {
            if (target == null || target.Transform == null || unitSO?.AttackConfig == null)
            {
                return;
            }

            if (!TryBeginLaunch())
            {
                return;
            }

            if (arrow == null)
            {
                ApplyDamage(target);
                return;
            }

            Vector3 startPosition = GetFirePosition();
            Vector3 endPosition = target.Transform.position + Vector3.up;
            arrowFlightRoutine = StartCoroutine(AnimateArrowFlight(startPosition, endPosition, target));
        }

        /// <summary>
        /// Gọi từ Animation Event (Engaging). Có thể bỏ qua nếu AttackTargetAction vừa gọi LaunchProjectile.
        /// </summary>
        public void OnFireArrow()
        {
            if (unitSO?.AttackConfig == null)
            {
                return;
            }

            if (!TryBeginLaunch())
            {
                return;
            }

            Vector3 startPosition = GetFirePosition();
            if (TryResolveAttackTarget(out Vector3 endPosition, out IDamageable damageable))
            {
                arrowFlightRoutine = StartCoroutine(AnimateArrowFlight(startPosition, endPosition, damageable));
                return;
            }

            endPosition = startPosition + transform.forward * unitSO.AttackConfig.AttackRange;
            damageable = FindDamageableNear(endPosition);
            arrowFlightRoutine = StartCoroutine(AnimateArrowFlight(startPosition, endPosition, damageable));
        }

        private bool TryBeginLaunch()
        {
            if (Time.time < lastLaunchTime + launchCooldownSeconds)
            {
                return false;
            }

            lastLaunchTime = Time.time;

            if (arrowFlightRoutine != null)
            {
                StopCoroutine(arrowFlightRoutine);
                arrowFlightRoutine = null;
            }

            return true;
        }

        private Vector3 GetFirePosition() =>
            firePoint != null ? firePoint.position : arrow.transform.position;

        private void CacheArrowHolster()
        {
            arrow = ResolveArrowInstance();
            if (arrow == null)
            {
                Debug.LogError($"Archer {name} is missing an arrow child. Assign the nested Arrow object, not the Arrow prefab asset.");
                return;
            }

            defaultArrowLocalPosition = arrow.transform.localPosition;
            defaultArrowLocalRotation = arrow.transform.localRotation;
            arrowParent = arrow.transform.parent;
        }

        private GameObject ResolveArrowInstance()
        {
            if (IsSceneHierarchyInstance(arrow))
            {
                return arrow;
            }

            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == "Arrow")
                {
                    return transforms[i].gameObject;
                }
            }

            return null;
        }

        private static bool IsSceneHierarchyInstance(GameObject candidate) =>
            candidate != null && candidate.scene.IsValid();

        private void ValidateArcherSetup()
        {
            if (unitSO?.AttackConfig == null)
            {
                Debug.LogError($"Archer {name} has no AttackConfig on UnitSO.");
                return;
            }

            if (!unitSO.AttackConfig.HasProjectileAttacks)
            {
                Debug.LogWarning(
                    $"Archer {name}: bật HasProjectileAttacks trên Attack Config để AttackTargetAction gọi LaunchProjectile.");
            }

            if (Owner == Owner.Invalid)
            {
                Debug.LogWarning(
                    $"Archer {name}: Owner is Invalid — vision ring ẩn cho đến khi Owner = Player1.");
            }
        }

        private bool TryResolveAttackTarget(out Vector3 endPosition, out IDamageable damageable)
        {
            endPosition = default;
            damageable = null;

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value != null)
            {
                Transform targetTransform = targetVariable.Value.transform;
                endPosition = targetTransform.position + Vector3.up;
                damageable = targetVariable.Value.GetComponentInParent<IDamageable>();
                return true;
            }

            if (graphAgent.GetVariable("Target", out BlackboardVariable<GameObject> attackTarget)
                && attackTarget.Value != null)
            {
                endPosition = attackTarget.Value.transform.position + Vector3.up;
                damageable = attackTarget.Value.GetComponentInParent<IDamageable>();
                return true;
            }

            if (graphAgent.GetVariable("TargetLocation", out BlackboardVariable<Vector3> targetLocationVariable))
            {
                endPosition = targetLocationVariable.Value;
                damageable = FindDamageableNear(endPosition);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Tìm IDamageable gần điểm trúng khi chỉ có vị trí (không có TargetGameObject).
        /// Cách hoạt động: OverlapSphere nhỏ trên DamageableLayers, bỏ qua đồng minh.
        /// </summary>
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

        private IEnumerator AnimateArrowFlight(Vector3 startPosition, Vector3 endPosition, IDamageable damageable)
        {
            arrow.transform.SetParent(null);
            arrow.transform.position = startPosition;

            float distance = Vector3.Distance(startPosition, endPosition);
            float duration = Mathf.Max(0.05f, distance / Mathf.Max(0.01f, projectileFlightSpeed));
            float elapsed = 0f;
            Vector3 previousPosition = startPosition;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                Vector3 position = EvaluateArcPosition(startPosition, endPosition, t);
                arrow.transform.position = position;

                Vector3 delta = position - previousPosition;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    arrow.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                }

                previousPosition = position;
                elapsed += Time.deltaTime;
                yield return null;
            }

            arrow.transform.position = endPosition;

            if (damageable == null)
            {
                damageable = FindDamageableNear(endPosition);
            }

            ApplyDamage(damageable);
            ResetArrowToHolster();
            arrowFlightRoutine = null;
        }

        private Vector3 EvaluateArcPosition(Vector3 start, Vector3 end, float t)
        {
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += arcHeight * 4f * t * (1f - t);
            return position;
        }

        private void ApplyDamage(IDamageable damageable)
        {
            if (damageable == null || damageable.Transform == null)
            {
                return;
            }

            if (damageable.Owner == Owner)
            {
                return;
            }

            damageable.TakeDamage(unitSO.AttackConfig.Damage);
        }

        private void ResetArrowToHolster()
        {
            if (arrowParent == null)
            {
                return;
            }

            arrow.transform.SetParent(arrowParent);
            arrow.transform.localPosition = defaultArrowLocalPosition;
            arrow.transform.localRotation = defaultArrowLocalRotation;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (arrowFlightRoutine != null)
            {
                StopCoroutine(arrowFlightRoutine);
            }

            if (IsSceneHierarchyInstance(arrow))
            {
                Destroy(arrow);
            }
        }
    }
}
