using System;
using GameDevTV.RTS.Game.Startup;
using UnityEngine;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Tạm dừng simulation in-game (Time.timeScale) và khôi phục tốc độ trước khi pause.
    /// </summary>
    public static class GamePauseService
    {
        public static event Action<bool> PauseStateChanged;

        public static bool IsPaused { get; private set; }

        static float _speedBeforePause = 1f;

        /// <summary>
        /// Mục tiêu: Dừng gameplay khi mở panel Pause.
        /// Cách hoạt động: Lưu timeScale hiện tại, gán 0; bỏ qua nếu đã pause.
        /// </summary>
        public static void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _speedBeforePause = Time.timeScale;
            if (_speedBeforePause <= 0f)
            {
                _speedBeforePause = 1f;
            }

            Time.timeScale = 0f;
            IsPaused = true;
            PauseStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Mục tiêu: Tiếp tục chơi sau panel Pause.
        /// Cách hoạt động: Khôi phục timeScale qua GameSpeedController nếu có, không thì gán trực tiếp.
        /// </summary>
        public static void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            ApplyStoredSpeed();
            PauseStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Mục tiêu: Đóng overlay và chạy game với tốc độ vừa chọn (ví dụ panel speed x0.5–x2).
        /// Cách hoạt động: Ghi tốc độ resume rồi Resume; nếu chưa pause thì chỉ ApplySpeed trực tiếp.
        /// </summary>
        public static void ResumeAtSpeed(float timeScale)
        {
            _speedBeforePause = Mathf.Max(0.0001f, timeScale);

            if (IsPaused)
            {
                Resume();
                return;
            }

            ApplyStoredSpeed();
        }

        static void ApplyStoredSpeed()
        {
            if (GameSpeedController.Instance != null)
            {
                GameSpeedController.Instance.ApplySpeed(_speedBeforePause);
                return;
            }

            Time.timeScale = _speedBeforePause;
            Time.fixedDeltaTime = 0.02f * Mathf.Max(_speedBeforePause, 0.0001f);
        }

        public static void ForceResumeForSceneChange()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
