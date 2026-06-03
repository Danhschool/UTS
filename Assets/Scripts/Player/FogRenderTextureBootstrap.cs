using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Khởi tạo RT fog (explored/vision) về đen một lần mỗi texture — vùng chưa khám phá hiện đúng trên plane.
    /// </summary>
    public static class FogRenderTextureBootstrap
    {
        static readonly HashSet<int> ClearedExploredIds = new();
        static readonly HashSet<int> ClearedVisionIds = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches()
        {
            ClearedExploredIds.Clear();
            ClearedVisionIds.Clear();
        }

        /// <summary>
        /// Mục tiêu: Explored RT bắt đầu đen — shader coi pixel &lt; threshold là chưa khám phá.
        /// Cách hoạt động: GL.Clear một lần theo GetInstanceID của RenderTexture.
        /// </summary>
        public static void EnsureExploredStartsBlack(RenderTexture explored)
        {
            ClearOnce(explored, ClearedExploredIds);
        }

        /// <summary>
        /// Mục tiêu: Vision RT bắt đầu đen — chỉ vùng unit nhìn thấy mới sáng.
        /// </summary>
        public static void EnsureVisionStartsBlack(RenderTexture vision)
        {
            ClearOnce(vision, ClearedVisionIds);
        }

        static void ClearOnce(RenderTexture target, HashSet<int> clearedSet)
        {
            if (target == null)
            {
                return;
            }

            int id = target.GetInstanceID();
            if (!clearedSet.Add(id))
            {
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previous;
        }
    }
}
