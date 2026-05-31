using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Điều khiển tốc độ simulation (1x / 2x) qua <see cref="Time.timeScale"/>.
    /// </summary>
    public sealed class GameSpeedController : MonoBehaviour
    {
        public static GameSpeedController Instance { get; private set; }

        [SerializeField] private float normalSpeed = 1f;
        [SerializeField] private float fastSpeed = 2f;
        [SerializeField] private float maxSpeed = 4f;
        [SerializeField] private bool enableKeyboardToggle = true;
        [SerializeField] private Key toggleFastSpeedKey = Key.NumpadPlus;
        [SerializeField] private Key resetNormalSpeedKey = Key.NumpadMinus;

        private const float BaseFixedDeltaTime = 0.02f;

        public float CurrentSpeed => Time.timeScale;
        public bool IsFastSpeed => Mathf.Approximately(Time.timeScale, fastSpeed);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            toggleFastSpeedKey = InputSystemKeyboardUtility.CoerceKeyboardKey(toggleFastSpeedKey, Key.NumpadPlus);
            resetNormalSpeedKey = InputSystemKeyboardUtility.CoerceKeyboardKey(resetNormalSpeedKey, Key.NumpadMinus);

            Instance = this;
            GameplayStartupGate.Unlocked += HandleGameplayUnlocked;
            TryApplyNormalSpeed();
        }

        void HandleGameplayUnlocked() => TryApplyNormalSpeed();

        void TryApplyNormalSpeed()
        {
            if (GameplayStartupGate.IsGameplayUnlocked)
            {
                ApplySpeed(normalSpeed);
            }
        }

        private void OnDestroy()
        {
            GameplayStartupGate.Unlocked -= HandleGameplayUnlocked;
            if (Instance == this)
            {
                Instance = null;
                ApplySpeed(normalSpeed);
            }
        }

        private void Update()
        {
            if (!enableKeyboardToggle)
            {
                return;
            }

            if (InputSystemKeyboardUtility.WasPressedThisFrame(toggleFastSpeedKey))
            {
                SetFastSpeed();
            }

            if (InputSystemKeyboardUtility.WasPressedThisFrame(resetNormalSpeedKey))
            {
                SetNormalSpeed();
            }
        }

        /// <summary>
        /// Mục tiêu: Trả game về 1x (tốc độ chuẩn).
        /// Cách hoạt động: Gán timeScale và fixedDeltaTime tương ứng.
        /// </summary>
        public void SetNormalSpeed() => ApplySpeed(normalSpeed);

        /// <summary>
        /// Mục tiêu: Tăng tốc simulation (mặc định 2x) để test AI/build nhanh hơn.
        /// Cách hoạt động: timeScale = fastSpeed; physics bước theo scale.
        /// </summary>
        public void SetFastSpeed() => ApplySpeed(fastSpeed);

        /// <summary>
        /// Mục tiêu: Bật 2x nếu đang 1x, ngược lại về 1x (một phím).
        /// </summary>
        public void ToggleFastSpeed()
        {
            if (IsFastSpeed)
            {
                SetNormalSpeed();
            }
            else
            {
                SetFastSpeed();
            }
        }

        /// <summary>
        /// Mục tiêu: Gán tốc độ tùy chỉnh (UI slider / debug).
        /// Cách hoạt động: Clamp và cập nhật fixedDeltaTime = 0.02 * scale.
        /// </summary>
        public void ApplySpeed(float scale)
        {
            float clamped = Mathf.Clamp(scale, 0f, maxSpeed);
            Time.timeScale = clamped;
            Time.fixedDeltaTime = BaseFixedDeltaTime * clamped;
        }
    }
}
