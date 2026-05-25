using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Map <see cref="Owner"/> → layer Unity cho Fog of War Plane (overlay Main Camera).
    /// </summary>
    public static class OwnerFogPlaneLayers
    {
        public const int DefaultPlayer1PlaneLayer = 13;
        public const int DefaultPlayer2PlaneLayer = 17;

        const string Player1LayerName = "Fog of War";
        const string Player2LayerName = "Fog of War Plane P2";

        /// <summary>
        /// Mục tiêu: Overlay gameplay chỉ vẽ plane fog của phe local (P1 layer 13, P2 layer riêng).
        /// Cách hoạt động: NameToLayer với fallback 13/17.
        /// </summary>
        public static int GetLayer(Owner owner)
        {
            if (owner == Owner.Player1)
            {
                int layer = LayerMask.NameToLayer(Player1LayerName);
                return layer >= 0 ? layer : DefaultPlayer1PlaneLayer;
            }

            if (owner == Owner.Player2)
            {
                int layer = LayerMask.NameToLayer(Player2LayerName);
                return layer >= 0 ? layer : DefaultPlayer2PlaneLayer;
            }

            return -1;
        }

        /// <summary>
        /// Mục tiêu: Gán layer cho mesh plane trên prefab Fog P1/P2 (tránh cả hai cùng layer 13).
        /// </summary>
        public static void ApplyToPlane(GameObject planeRoot, Owner owner)
        {
            if (planeRoot == null || !HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            int layer = GetLayer(owner);
            if (layer < 0)
            {
                return;
            }

            planeRoot.layer = layer;
        }
    }
}
