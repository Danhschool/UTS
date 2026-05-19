using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [RequireComponent(typeof(SphereCollider))]
    public class DamageableSensor : MonoBehaviour
    {
        public List<IDamageable> Damageables
        {
            get
            {
                PruneDestroyedDamageables();
                return visibleDamageables.ToList();
            }
        }
        [field: SerializeField] public Owner Owner { get; set; }

        public delegate void UnitDetectionEvent(IDamageable damageable);
        public event UnitDetectionEvent OnUnitEnter;
        public event UnitDetectionEvent OnUnitExit;

        private new SphereCollider collider;
        private HashSet<IDamageable> visibleDamageables = new();
        private HashSet<IDamageable> allDamageables = new();

        private void Awake()
        {
            collider = GetComponent<SphereCollider>();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (collider.TryGetComponent(out IDamageable damageable) && damageable.Owner != Owner)
            {
                allDamageables.Add(damageable);
                if (collider.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged += HandleVisibilityChange;
                    if (hideable.IsVisible)
                    {
                        visibleDamageables.Add(damageable);
                        OnUnitEnter?.Invoke(damageable);
                    }
                }
                else
                {
                    visibleDamageables.Add(damageable);
                    OnUnitEnter?.Invoke(damageable);
                }
            }

            if (allDamageables.Count == 1)
            {
                Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            }
        }

        private void OnTriggerExit(Collider collider)
        {
            if (collider.TryGetComponent(out IDamageable damageable)
                && allDamageables.Remove(damageable) && visibleDamageables.Remove(damageable))
            {
                OnUnitExit?.Invoke(damageable);
            }

            if (collider.TryGetComponent(out IHideable hideable))
            {
                hideable.OnVisibilityChanged -= HandleVisibilityChange;
            }

            if (allDamageables.Count == 0)
            {
                Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            }
        }

        private void OnDestroy()
        {
            // Unit trong trigger có thể đã Destroy mà không có OnTriggerExit — Transform / object có thể null.
            foreach (IDamageable damageable in allDamageables.ToArray())
            {
                if (damageable is UnityEngine.Object unityRef && unityRef == null)
                {
                    continue;
                }

                Transform t = damageable.Transform;
                if (t == null)
                {
                    continue;
                }

                if (t.TryGetComponent(out IHideable hideable))
                {
                    hideable.OnVisibilityChanged -= HandleVisibilityChange;
                }
            }

            allDamageables.Clear();
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
        }

        private void HandleVisibilityChange(IHideable hideable, bool isVisible)
        {
            IDamageable damageable = hideable.Transform.GetComponent<IDamageable>();
            if (isVisible)
            {
                visibleDamageables.Add(damageable);
                OnUnitEnter?.Invoke(damageable);
            }
            else
            {
                visibleDamageables.Remove(damageable);
                OnUnitExit?.Invoke(damageable);
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            PruneDestroyedDamageables();

            if (evt.Unit == null || !allDamageables.Contains(evt.Unit))
            {
                return;
            }

            Collider unitCollider = evt.Unit.GetComponent<Collider>();
            if (unitCollider != null)
            {
                OnTriggerExit(unitCollider);
                return;
            }

            if (visibleDamageables.Remove(evt.Unit))
            {
                OnUnitExit?.Invoke(evt.Unit);
            }

            allDamageables.Remove(evt.Unit);
            if (allDamageables.Count == 0)
            {
                Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            }
        }

        /// <summary>
        /// Mục tiêu: Loại reference tới unit đã Destroy khỏi sensor (Unity không luôn gọi OnTriggerExit).
        /// Cách hoạt động: Duyệt bản sao HashSet, xóa entry có Unity object hoặc Transform null.
        /// </summary>
        private void PruneDestroyedDamageables()
        {
            foreach (IDamageable damageable in visibleDamageables.ToArray())
            {
                if (!IsValidDamageable(damageable))
                {
                    visibleDamageables.Remove(damageable);
                    allDamageables.Remove(damageable);
                }
            }
        }

        private static bool IsValidDamageable(IDamageable damageable)
        {
            if (damageable is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            return damageable != null && damageable.Transform != null;
        }

        public void SetupFrom(AttackConfigSO attackConfig)
        {
            collider.radius = attackConfig.AttackRange;
        }
    }
}
