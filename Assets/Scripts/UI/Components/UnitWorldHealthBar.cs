using System.Collections;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Thanh máu world-space trên unit: lắng nghe <see cref="AbstractCommandable.OnHealthUpdated"/> và cập nhật <see cref="ProgressBar"/>.
    /// </summary>
    public class UnitWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private AbstractCommandable commandable;
        [SerializeField] private ProgressBar progressBar;
        [Tooltip("Để trống sẽ dùng Camera.main một lần trong Awake (nên gán camera gameplay trong Inspector nếu có nhiều camera).")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool billboardTowardCamera = true;

        private void Awake()
        {
            if (commandable == null)
            {
                commandable = GetComponentInParent<AbstractCommandable>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (commandable == null || progressBar == null)
            {
                return;
            }

            commandable.OnHealthUpdated += HandleHealthUpdated;
            StartCoroutine(RefreshAfterHealthInitialized());
        }

        /// <summary>
        /// Mục tiêu: vẽ đúng thanh máu lúc spawn dù <see cref="UnitWorldHealthBar"/> Start chạy trước <see cref="AbstractUnit.Start"/>.
        /// Cách hoạt động: chờ hết frame (sau batch Start), rồi gọi <see cref="ApplyFillFromHealth"/>.
        /// </summary>
        private IEnumerator RefreshAfterHealthInitialized()
        {
            yield return null;
            ApplyFillFromHealth();
        }

        private void OnDestroy()
        {
            if (commandable != null)
            {
                commandable.OnHealthUpdated -= HandleHealthUpdated;
            }
        }

        private void LateUpdate()
        {
            if (!billboardTowardCamera || worldCamera == null)
            {
                return;
            }

            Vector3 toCamera = transform.position - worldCamera.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera);
            }
        }

        /// <summary>
        /// Mục tiêu: giữ thanh máu khớp với <see cref="AbstractCommandable.CurrentHealth"/> / <see cref="AbstractCommandable.MaxHealth"/>.
        /// Cách hoạt động: tính tỉ lệ 0–1, gọi <see cref="ProgressBar.SetProgress"/> (cùng API với UI xây nhà trong project).
        /// </summary>
        private void ApplyFillFromHealth()
        {
            if (progressBar == null || commandable == null)
            {
                return;
            }

            float ratio = commandable.MaxHealth > 0
                ? Mathf.Clamp01((float)commandable.CurrentHealth / commandable.MaxHealth)
                : 0f;

            progressBar.SetProgress(ratio);
        }

        private void HandleHealthUpdated(AbstractCommandable _, int __, int ___)
        {
            ApplyFillFromHealth();
        }
    }
}
