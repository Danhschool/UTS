using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Tìm component Supplies HUD đúng phe (Runtime UI UGUI vs (1)).
    /// </summary>
    public static class MpHudSuppliesResolver
    {
        /// <summary>
        /// Mục tiêu: P2 client bind HUD "(1)" thay vì nhầm Supplies P1.
        /// Cách hoạt động: P2 ưu tiên tên chứa "(1)"; P1 ưu tiên không có "(1)".
        /// </summary>
        public static Supplies FindForOwner(Owner owner)
        {
            Supplies[] all = Object.FindObjectsByType<Supplies>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Supplies fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                Supplies candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                MpPlayerPresentationRig rig = candidate.GetComponentInParent<MpPlayerPresentationRig>(true);
                if (rig != null && rig.PresentationOwner == owner)
                {
                    return candidate;
                }

                bool isDuplicateName = candidate.gameObject.name.Contains("(1)");
                if (owner == Owner.Player2)
                {
                    if (isDuplicateName)
                    {
                        return candidate;
                    }
                }
                else if (!isDuplicateName)
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }
    }
}
