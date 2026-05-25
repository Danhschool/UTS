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

        /// <summary>
        /// Mục tiêu: Gán layer cho mọi con Vision (kể cả inactive) — prefab Vision.prefab mặc định layer 14.
        /// Cách hoạt động: GetComponentsInChildren(true) trên VisionTransform rồi set layer theo owner.
        /// </summary>
        public static void ApplyToCommandableVision(AbstractCommandable commandable)
        {
            if (commandable == null || !HumanFogVisionUtility.EmitsFogVision(commandable.Owner))
            {
                return;
            }

            Transform visionRoot = commandable.VisionTransformRoot;
            if (visionRoot == null)
            {
                return;
            }

            ApplyToHierarchy(visionRoot.gameObject, commandable.Owner);
        }

        static void SetLayerRecursive(Transform root, int layer)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }
    }
}
