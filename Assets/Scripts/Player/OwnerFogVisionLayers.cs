using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Map <see cref="Owner"/> human → layer Unity cho VisionTransform / fog cameras.
    /// </summary>
    public static class OwnerFogVisionLayers
    {
        public const int DefaultPlayer1VisionLayer = 14;
        public const int DefaultPlayer2VisionLayer = 15;

        const string Player1LayerName = "Fog of War Vision";
        const string Player2LayerName = "Fog Vision Player2";

        /// <summary>
        /// Mục tiêu: Layer index cho VisionTransform và culling mask camera fog.
        /// Cách hoạt động: NameToLayer với fallback 14/15 nếu chưa khai báo trong Project Settings.
        /// </summary>
        public static int GetLayer(Owner owner)
        {
            if (owner == Owner.Player1)
            {
                int layer = LayerMask.NameToLayer(Player1LayerName);
                return layer >= 0 ? layer : DefaultPlayer1VisionLayer;
            }

            if (owner == Owner.Player2)
            {
                int layer = LayerMask.NameToLayer(Player2LayerName);
                return layer >= 0 ? layer : DefaultPlayer2VisionLayer;
            }

            return -1;
        }

        /// <summary>
        /// Mục tiêu: Gán layer cho cây VisionTransform (và con) khi spawn/refresh sight.
        /// Cách hoạt động: Đệ quy <see cref="GameObject.layer"/> trên root và children.
        /// </summary>
        public static void ApplyToHierarchy(GameObject root, Owner owner)
        {
            if (root == null || !HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            int layer = GetLayer(owner);
            if (layer < 0)
            {
                return;
            }

            SetLayerRecursive(root.transform, layer);
        }

        static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursive(root.GetChild(i), layer);
            }
        }
    }
}
