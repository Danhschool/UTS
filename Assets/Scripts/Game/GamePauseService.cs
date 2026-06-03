using System;
using UnityEngine;

namespace GameDevTV.RTS.Game
{
    /// <summary>
    /// SRP: Tạm dừng simulation in-game (Time.timeScale) — tách pause chia sẻ MP và pause local (settings).
    /// </summary>
    public static class GamePauseService
    {
        public static event Action<bool> PauseStateChanged;

        public static bool IsPaused { get; private set; }

        static bool _sharedSimulationPaused;
        static bool _localSettingsPaused;
        static float _simulationSpeed = 1f;

        /// <summary>
        /// Mục tiêu: Pause chia sẻ (menu pause / dialog đầu hàng) — đồng bộ qua network.
        /// Cách hoạt động: Ghi cờ shared rồi RefreshSimulationTime.
        /// </summary>
        public static void ApplySharedPause(bool paused)
        {
            _sharedSimulationPaused = paused;
            RefreshSimulationTime();
        }

        /// <summary>
        /// Mục tiêu: Pause chỉ trên máy mở Settings — không ảnh hưởng đối thủ.
        /// Cách hoạt động: Ghi cờ local settings rồi RefreshSimulationTime.
        /// </summary>
        public static void ApplyLocalSettingsPause(bool paused)
        {
            _localSettingsPaused = paused;
            RefreshSimulationTime();
        }

        /// <summary>
        /// Mục tiêu: Áp tốc độ simulation đã đồng bộ (MP) hoặc local (offline).
        /// Cách hoạt động: Lưu speed rồi RefreshSimulationTime nếu không đang pause.
        /// </summary>
        public static void ApplySharedSpeed(float timeScale)
        {
            _simulationSpeed = Mathf.Max(0.0001f, timeScale);
            RefreshSimulationTime();
        }

        /// <summary>
        /// Mục tiêu: Dừng gameplay khi mở panel Pause (offline hoặc fallback).
        /// </summary>
        public static void Pause() => ApplySharedPause(true);

        /// <summary>
        /// Mục tiêu: Tiếp tục chơi sau panel Pause.
        /// </summary>
        public static void Resume()
        {
            ApplySharedPause(false);
            ApplyLocalSettingsPause(false);
        }

        /// <summary>
        /// Mục tiêu: Đóng overlay và chạy game với tốc độ vừa chọn.
        /// </summary>
        public static void ResumeAtSpeed(float timeScale)
        {
            ApplySharedSpeed(timeScale);
            ApplySharedPause(false);
            ApplyLocalSettingsPause(false);
        }

        static void RefreshSimulationTime()
        {
            bool paused = _sharedSimulationPaused || _localSettingsPaused;
            if (paused)
            {
                if (!IsPaused)
                {
                    IsPaused = true;
                    PauseStateChanged?.Invoke(true);
                }

                Time.timeScale = 0f;
                return;
            }

            if (IsPaused)
            {
                IsPaused = false;
                PauseStateChanged?.Invoke(false);
            }

            ApplySimulationSpeed(_simulationSpeed);
        }

        static void ApplySimulationSpeed(float scale)
        {
            if (GameSpeedController.Instance != null)
            {
                GameSpeedController.Instance.ApplySpeed(scale);
                return;
            }

            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;
        }

        public static void ForceResumeForSceneChange()
        {
            _sharedSimulationPaused = false;
            _localSettingsPaused = false;
            IsPaused = false;
            _simulationSpeed = 1f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
