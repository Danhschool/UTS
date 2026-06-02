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

        [SerializeField] private float slowSpeed = 0.5f;
        [SerializeField] private float normalSpeed = 1f;
        [SerializeField] private float fastSpeed = 1.5f;
        [SerializeField] private float veryFastSpeed = 2f;
        [SerializeField] private float maxSpeed = 4f;
        [SerializeField] private bool enableKeyboardToggle = true;
        [SerializeField] private Key toggleFastSpeedKey = Key.NumpadPlus;
        [SerializeField] private Key resetNormalSpeedKey = Key.NumpadMinus;

        private const float BaseFixedDeltaTime = 0.02f;

        public float CurrentSpeed => Time.timeScale;
        public bool IsFastSpeed => Mathf.Approximately(Time.timeScale, veryFastSpeed);

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
        public void SetFastSpeed() => ApplySpeed(veryFastSpeed);

        /// <summary>
        /// Mục tiêu: UI in-game chọn preset tốc độ (0.5x / 1x / 1.5x / 2x).
        /// Cách hoạt động: Clamp qua ApplySpeed; bỏ qua khi đang pause (timeScale=0).
        /// </summary>
        public void ApplyPresetSpeed(float scale)
        {
            if (GamePauseService.IsPaused)
            {
                return;
            }

            ApplySpeed(scale);
        }

        public void SetHalfSpeed() => ApplyPresetSpeed(slowSpeed);
        public void SetNormalSpeedPreset() => ApplyPresetSpeed(normalSpeed);
        public void SetFastSpeedPreset() => ApplyPresetSpeed(fastSpeed);
        public void SetDoubleSpeed() => ApplyPresetSpeed(veryFastSpeed);

        /// <summary>
        /// Mục tiêu: Lấy hệ số timeScale theo index nút UI (0–3).
        /// Cách hoạt động: Map index sang các preset đã cấu hình trong Inspector.
        /// </summary>
        public float GetPresetScale(int index)
        {
            return index switch
            {
                0 => slowSpeed,
                1 => normalSpeed,
                2 => fastSpeed,
                3 => veryFastSpeed,
                _ => normalSpeed
            };
        }

        /// <summary>
        /// Mục tiêu: Bật 2x nếu đang 1x, ngược lại về 1x (một phím).
        /// </summary>
        public void ToggleFastSpeed()
        {
            if (Mathf.Approximately(Time.timeScale, veryFastSpeed))
            {
                SetNormalSpeed();
            }
            else
            {
                SetDoubleSpeed();
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
