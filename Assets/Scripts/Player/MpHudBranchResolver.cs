using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Xác định nhánh HUD P1/P2 khi RuntimeUI không nằm dưới transform Fog (chỉ link qua hudRoot SerializeField).
    /// </summary>
    public static class MpHudBranchResolver
    {
        /// <summary>
        /// Mục tiêu: Map RuntimeUI / Supplies tới Owner presentation (P1 hoặc P2).
        /// Cách hoạt động: Parent rig → owns hudRoot/supplies → tên chứa "(1)".
        /// </summary>
        public static Owner ResolveFor(Transform hudLeaf)
        {
            if (hudLeaf == null)
            {
                return Owner.Invalid;
            }

            MpPlayerPresentationRig parentRig = hudLeaf.GetComponentInParent<MpPlayerPresentationRig>(true);
            if (parentRig != null)
            {
                return parentRig.PresentationOwner;
            }

            MpPlayerPresentationRig[] rigs = Object.FindObjectsByType<MpPlayerPresentationRig>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < rigs.Length; i++)
            {
                MpPlayerPresentationRig rig = rigs[i];
                if (rig != null && rig.OwnsHudTransform(hudLeaf))
                {
                    return rig.PresentationOwner;
                }
            }

            return InferFromHierarchyName(hudLeaf);
        }

        static Owner InferFromHierarchyName(Transform t)
        {
            while (t != null)
            {
                if (t.name.Contains("(1)"))
                {
                    return Owner.Player2;
                }

                t = t.parent;
            }

            return Owner.Player1;
        }
    }
}
