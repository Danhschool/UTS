#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.Game.Pregame
{
    /// <summary>
    /// SRP: Thoát game / dừng Play mode trong Editor.
    /// </summary>
    public static class PregameApplicationQuit
    {
        /// <summary>
        /// Mục tiêu: Xử lý nút Thoát trên menu và màn setup.
        /// Cách hoạt động: Editor dừng Play mode; build gọi Application.Quit().
        /// </summary>
        public static void RequestQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
