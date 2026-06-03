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

        /// <summary>Human P2 dùng chung layer vision 14; tách phe bằng RT P1/P2 (xem PLAN_FOG_MP).</summary>
        public const int DefaultPlayer2VisionLayer = DefaultPlayer1VisionLayer;

        const string HumanVisionLayerName = "Fog of War Vision";
        const string DedicatedPlayer2VisionLayerName = "Fog Vision Player2";

        /// <summary>
        /// Mục tiêu: Layer index cho VisionTransform và culling mask camera fog human.
        /// Cách hoạt động: P1/P2 → Fog of War Vision (14); tách phe bằng RT + chỉ unit local bật Vision.
        /// </summary>
        public static int GetLayer(Owner owner)
        {
            if (owner == Owner.Player1 || owner == Owner.Player2)
            {
                int layer = LayerMask.NameToLayer(HumanVisionLayerName);
                return layer >= 0 ? layer : DefaultPlayer1VisionLayer;
            }

            return -1;
        }

        /// <summary>
        /// Mục tiêu: Layer 15 trong TagManager (Fog Vision Player2) — minimap loại trừ, không gán lên unit human.
        /// Cách hoạt động: NameToLayer; trả -1 nếu trùng layer human hoặc chưa khai báo.
        /// </summary>
        public static int GetDedicatedPlayer2VisionLayerIndex()
        {
            int dedicated = LayerMask.NameToLayer(DedicatedPlayer2VisionLayerName);
            if (dedicated < 0)
            {
                return -1;
            }

            int human = GetLayer(Owner.Player2);
            return dedicated == human ? -1 : dedicated;
        }

        /// <summary>
        /// Mục tiêu: Gán layer cho cây VisionTransform (và con) khi spawn/refresh sight.
        /// Cách hoạt động: Đệ quy <see cref="GameObject.layer"/> trên root và children.
        /// </summary>
        public static void ApplyToHierarchy(GameObject root, Owner owner)
        {
            if (root == null || !HumanFogVisionUtility.EmitsFogVisionOnThisClient(owner))
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
            if (commandable == null || !HumanFogVisionUtility.EmitsFogVisionOnThisClient(commandable.Owner))
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
