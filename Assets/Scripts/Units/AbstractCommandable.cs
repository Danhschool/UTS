using System;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.UI.Components;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameDevTV.RTS.Units
{
    public abstract class AbstractCommandable : MonoBehaviour, ISelectable, IDamageable, IHideable
    {
        [field: SerializeField] public bool IsSelected { get; protected set; }
        [field: SerializeField] public int CurrentHealth { get; protected set; }
        [field: SerializeField] public int MaxHealth { get; protected set; }
        [field: SerializeField] public Owner Owner { get; set; }
        [field: SerializeField] public bool IsVisible { get; private set; } = true;
        public Transform Transform => this == null ? null : transform;
        [field: SerializeField] public BaseCommand[] AvailableCommands { get; private set; }
        [field: SerializeField] public AbstractUnitSO UnitSO { get; private set; }
        [SerializeField] protected DecalProjector decalProjector;
        [SerializeField] protected Transform VisionTransform;

        /// <summary>Root vòng nhìn fog (Vision.prefab + con) — dùng khi gán layer MP.</summary>
        public Transform VisionTransformRoot => VisionTransform;

        public delegate void HealthUpdatedEvent(AbstractCommandable commandable, int lastHealth, int newHealth);
        public event HealthUpdatedEvent OnHealthUpdated;
        
        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private BaseCommand[] initialCommands;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        private Animator[] animators = Array.Empty<Animator>();
        private AnimatorCullingMode[] savedAnimatorCullingModes = Array.Empty<AnimatorCullingMode>();
        private bool deathSequenceStarted;

        protected virtual void Awake()
        {
            if (UnitSO != null)
            {
                UnitSO = UnitSO.Clone() as AbstractUnitSO;
            }

            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();
            animators = GetComponentsInChildren<Animator>();
            EnsureInitialCommandsCached();
        }

        protected virtual void Start()
        {
            if (AvailableCommands == null)
            {
                AvailableCommands = Array.Empty<BaseCommand>();
            }

            EnsureInitialCommandsCached();

            Bus<UpgradeResearchedEvent>.OnEvent[Owner] += HandleUpgradeResearched;
        }

        /// <summary>
        /// Mục tiêu: Giữ bản gốc AvailableCommands từ prefab trước khi Start (nhà xây có component tắt đến khi xong).
        /// Cách hoạt động: Lưu mảng lệnh lần đầu có phần tử; Deselect không được ghi đè bằng mảng rỗng.
        /// </summary>
        private void EnsureInitialCommandsCached()
        {
            if (initialCommands != null && initialCommands.Length > 0)
            {
                return;
            }

            if (AvailableCommands != null && AvailableCommands.Length > 0)
            {
                initialCommands = AvailableCommands;
            }
        }

        /// <summary>
        /// Mục tiêu: MP/offline spawn gán Owner rồi bật đúng VisionTransform + layer fog.
        /// Cách hoạt động: Set <see cref="Owner"/> và gọi <see cref="RefreshVisionFromSightConfig"/>.
        /// </summary>
        public void SyncOwnerAndFogVision(Owner owner)
        {
            Owner = owner;
            RefreshVisionFromSightConfig();
            ApplyFogVisionLayersToVisionHierarchy();
        }

        /// <summary>
        /// Mục tiêu: P2 spawn — con trong Vision.prefab không còn kẹt layer 14 (P1).
        /// Cách hoạt động: Đệ quy mọi Transform dưới VisionTransform theo <see cref="Owner"/>.
        /// </summary>
        protected void ApplyFogVisionLayersToVisionHierarchy()
        {
            OwnerFogVisionLayers.ApplyToCommandableVision(this);
        }

        /// <summary>
        /// Cập nhật collider / scale vòng nhìn sau khi <see cref="SightConfigSO.SightRadius"/> thay đổi (upgrade).
        /// </summary>
        protected void RefreshVisionFromSightConfig()
        {
            if (VisionTransform == null)
            {
                return;
            }

            bool emitsVision = HumanFogVisionUtility.EmitsFogVision(Owner);
            VisionTransform.gameObject.SetActive(emitsVision);

            if (!emitsVision)
            {
                return;
            }

            if (UnitSO != null && UnitSO.SightConfig != null)
            {
                float size = UnitSO.SightConfig.SightRadius * 2;
                VisionTransform.localScale = new Vector3(size, size, size);
            }

            ApplyFogVisionLayersToVisionHierarchy();
        }

        /// <summary>
        /// Đồng bộ MaxHealth với <see cref="AbstractUnitSO.Health"/>; nếu max tăng thì cộng phần tăng vào CurrentHealth.
        /// </summary>
        protected void SyncRuntimeHealthFromUnitSo(bool healAddedMaxPortion)
        {
            int newMax = UnitSO.Health;
            int oldMax = MaxHealth;
            int oldCur = CurrentHealth;
            MaxHealth = newMax;

            if (healAddedMaxPortion && newMax > oldMax)
            {
                CurrentHealth += newMax - oldMax;
            }
            else
            {
                CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            }

            if (oldCur != CurrentHealth || oldMax != MaxHealth)
            {
                OnHealthUpdated?.Invoke(this, oldCur, CurrentHealth);
            }
        }

        /// <summary>
        /// Đơn vị kế thừa ghi đè để cập nhật NavMeshAgent, sensor đánh, blackboard sau research.
        /// </summary>
        protected virtual void OnUpgradeAppliedToRuntime() { }

        protected virtual void OnDestroy()
        {
            Bus<UpgradeResearchedEvent>.OnEvent[Owner] -= HandleUpgradeResearched;
        }

        public virtual void Select()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(true);
            }

            IsSelected = true;
            Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
        }

        public virtual void Deselect()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(false);
            }

            IsSelected = false;
            SetCommandOverrides(null);

            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        public void SetCommandOverrides(BaseCommand[] commands)
        {
            EnsureInitialCommandsCached();

            if (commands == null || commands.Length == 0)
            {
                AvailableCommands = initialCommands ?? Array.Empty<BaseCommand>();
            }
            else
            {
                AvailableCommands = commands;
            }

            if (IsSelected)
            {
                Bus<UnitSelectedEvent>.Raise(Owner, new UnitSelectedEvent(this));
            }
        }

        public bool IsInDeathSequence => deathSequenceStarted;

        internal void MarkDeathSequenceStarted() => deathSequenceStarted = true;

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, null);
        }

        /// <summary>
        /// Mục tiêu: Trừ máu và cho phép phản ứng theo nguồn sát thương (vd. animal counter-attack).
        /// Cách hoạt động: Gọi overload có attacker; lớp con override để đổi Command ngay khi bị đánh.
        /// </summary>
        public virtual void TakeDamage(int damage, IDamageable attacker)
        {
            if (deathSequenceStarted)
            {
                return;
            }

            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, CurrentHealth);

            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
            if (CurrentHealth == 0)
            {
                Die();
            }
        }

        public virtual void Die()
        {
            Destroy(gameObject);
        }

        public void Heal(int amount)
        {
            int lastHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, MaxHealth);
            OnHealthUpdated?.Invoke(this, lastHealth, CurrentHealth);
        }

        public void SetVisible(bool isVisible)
        {
            if (isVisible == IsVisible) return;

            IsVisible = isVisible;
            OnVisibilityChanged?.Invoke(this, isVisible);

            if (IsVisible)
            {
                OnGainVisibility();
            }
            else
            {
                OnLoseVisibility();
            }
        }

        protected virtual void OnGainVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.gameObject.SetActive(true);
                }
            }

            RestoreAnimatorCullingAfterFog();
        }

        protected virtual void OnLoseVisibility()
        {
            KeepAnimatorsUpdatingWhileFogHidden();

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Animation vẫn chạy khi unit bị ẩn bởi fog (renderer tắt).
        /// Cách hoạt động: Unity dừng cập nhật xương nếu không render — ép AlwaysAnimate và lưu mode cũ.
        /// </summary>
        private void KeepAnimatorsUpdatingWhileFogHidden()
        {
            savedAnimatorCullingModes = new AnimatorCullingMode[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                savedAnimatorCullingModes[i] = animator.cullingMode;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        /// <summary>
        /// Mục tiêu: Khôi phục culling Animator và pose đúng khi unit hiện lại sau fog.
        /// Cách hoạt động: Trả lại culling mode đã lưu và ép Animator cập nhật một frame.
        /// </summary>
        private void RestoreAnimatorCullingAfterFog()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                if (i < savedAnimatorCullingModes.Length)
                {
                    animator.cullingMode = savedAnimatorCullingModes[i];
                }

                animator.Update(0f);
            }
        }

        private void HandleUpgradeResearched(UpgradeResearchedEvent evt)
        {
            if (evt.Owner == Owner && UnitSO.Upgrades.Contains(evt.Upgrade))
            {
                evt.Upgrade.Apply(UnitSO);
                SyncRuntimeHealthFromUnitSo(healAddedMaxPortion: true);
                RefreshVisionFromSightConfig();
                OnUpgradeAppliedToRuntime();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnitWorldHealthBar[] healthBars = GetComponentsInChildren<UnitWorldHealthBar>(true);
            foreach (UnitWorldHealthBar healthBar in healthBars)
            {
                healthBar.RefreshOwnerStyleInEditor();
            }

            if (Application.isPlaying && HumanFogVisionUtility.EmitsFogVision(Owner))
            {
                RefreshVisionFromSightConfig();
            }
        }
#endif
    }
}
