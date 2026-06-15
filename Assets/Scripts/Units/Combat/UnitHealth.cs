using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.Components;
using UnityEngine;

namespace GameDevTV.RTS.Units.Combat
{
    /// <summary>
    /// SRP: Đồng bộ thanh máu world-space từ <see cref="AbstractCommandable"/> — null-safe cho MP combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitHealth : MonoBehaviour
    {
        [SerializeField] AbstractCommandable commandable;
        [SerializeField] ProgressBar progressBar;
        [SerializeField] UnitWorldHealthBar worldHealthBar;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (commandable != null)
            {
                commandable.OnHealthUpdated += HandleHealthUpdated;
                SyncHealth(commandable.CurrentHealth, commandable.MaxHealth);
            }
        }

        void OnDisable()
        {
            if (commandable != null)
            {
                commandable.OnHealthUpdated -= HandleHealthUpdated;
            }
        }

        void ResolveReferences()
        {
            commandable ??= GetComponentInParent<AbstractCommandable>();
            progressBar ??= GetComponentInChildren<ProgressBar>(true);
            worldHealthBar ??= GetComponentInChildren<UnitWorldHealthBar>(true);
        }

        void HandleHealthUpdated(AbstractCommandable _, int lastHealth, int newHealth)
        {
            int max = commandable != null ? commandable.MaxHealth : 0;
            SyncHealth(newHealth, max);
        }

        /// <summary>
        /// Mục tiêu: Cập nhật fill thanh máu mà không NullRef khi prefab thiếu reference.
        /// Cách hoạt động: Bỏ qua nếu progressBar null; clamp ratio 0–1.
        /// </summary>
        public void SyncHealth(float currentHealth, float maxHealth)
        {
            if (progressBar == null)
            {
                return;
            }

            float ratio = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;

            progressBar.SetProgress(ratio);
        }
    }
}
