using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Game 1/2 MP — chỉ bật HUD canvas đúng phe; tắt Runtime UI UGUI đối thủ.
    /// </summary>
    public static class MpPresentationHudGuard
    {
        /// <summary>
        /// Mục tiêu: Client P2 không bị HUD P1 che hoặc bind nhầm supplies/action bar.
        /// Cách hoạt động: Theo nhánh P1/P2 (rig hoặc tên root "(1)"); active đúng một root.
        /// </summary>
        public static void ApplyLocalHudBranch(Owner localOwner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            Supplies[] allSupplies = Object.FindObjectsByType<Supplies>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < allSupplies.Length; i++)
            {
                Supplies supplies = allSupplies[i];
                if (supplies == null)
                {
                    continue;
                }

                Owner branchOwner = MpHudBranchResolver.ResolveFor(supplies.transform);
                if (branchOwner == Owner.Invalid)
                {
                    continue;
                }

                bool isLocalBranch = branchOwner == localOwner;
                GameObject hudRoot = supplies.transform.root.gameObject;

                if (hudRoot.activeSelf != isLocalBranch)
                {
                    hudRoot.SetActive(isLocalBranch);
                }

                if (isLocalBranch)
                {
                    supplies.BindHudOwner(localOwner);
                    WireRuntimeUiBranch(hudRoot, localOwner);
                }
                else
                {
                    UnwireRuntimeUiBranch(hudRoot);
                }
            }
        }

        static void WireRuntimeUiBranch(GameObject hudRoot, Owner owner)
        {
            RuntimeUI[] runtimeUis = hudRoot.GetComponentsInChildren<RuntimeUI>(true);
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui == null || ui.IsConfiguredForOwner(owner))
                {
                    continue;
                }

                ui.ConfigureBusOwner(owner);
            }
        }

        static void UnwireRuntimeUiBranch(GameObject hudRoot)
        {
            RuntimeUI[] runtimeUis = hudRoot.GetComponentsInChildren<RuntimeUI>(true);
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                runtimeUis[i]?.ReleaseBusSubscription();
            }
        }
    }
}
