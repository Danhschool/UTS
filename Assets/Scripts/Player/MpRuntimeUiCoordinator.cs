using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Đồng bộ refresh HUD MP (supplies, selection, building queue/progress) trên pure client.
    /// </summary>
    public static class MpRuntimeUiCoordinator
    {
        /// <summary>
        /// Mục tiêu: P2 HUD giống P1 — supplies + panel đang chọn sau snapshot / presentation apply.
        /// Cách hoạt động: Bind Supplies đúng phe rồi refresh RuntimeUI active.
        /// </summary>
        public static void RefreshFullPresentationForOwner(Owner owner)
        {
            RefreshSuppliesHudForOwner(owner);
            RefreshSelectionUiForOwner(owner);
        }

        /// <summary>
        /// Mục tiêu: Thanh S/W/F/Population client MP cập nhật sau SyncVar.
        /// </summary>
        public static void RefreshSuppliesHudForOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.IsHumanPlayer(owner))
            {
                return;
            }

            Supplies hud = MpHudSuppliesResolver.FindForOwner(owner);
            if (hud == null)
            {
                return;
            }

            if (!hud.gameObject.activeInHierarchy)
            {
                hud.gameObject.SetActive(true);
            }

            hud.BindHudOwner(owner);
        }

        /// <summary>
        /// Mục tiêu: Action bar / panel chọn unit cập nhật affordability sau SyncVar supplies.
        /// </summary>
        public static void RefreshSelectionUiForOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.IsHumanPlayer(owner))
            {
                return;
            }

            RuntimeUI[] runtimeUis = Object.FindObjectsByType<RuntimeUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui == null)
                {
                    continue;
                }

                if (!ui.isActiveAndEnabled)
                {
                    continue;
                }

                if (!ui.IsConfiguredForOwner(owner)
                    && MpHudBranchResolver.ResolveFor(ui.transform) == owner)
                {
                    ui.ConfigureBusOwner(owner);
                }

                if (!ui.IsConfiguredForOwner(owner))
                {
                    continue;
                }

                ui.RefreshSelectionPresentation();
            }
        }

        /// <summary>
        /// Mục tiêu: Progress bar xây nhà / hàng đợi train trên client khi SyncVar building đổi.
        /// </summary>
        public static void RefreshBuildingPresentation(BaseBuilding building)
        {
            if (building == null)
            {
                return;
            }

            Owner owner = LocalCommandableOwnership.ResolveOwner(building);
            if (!HumanFogVisionUtility.IsHumanPlayer(owner))
            {
                return;
            }

            RuntimeUI[] runtimeUis = Object.FindObjectsByType<RuntimeUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui == null || !ui.IsConfiguredForOwner(owner))
                {
                    continue;
                }

                ui.RefreshBuildingIfSelected(building);
            }
        }
    }
}
