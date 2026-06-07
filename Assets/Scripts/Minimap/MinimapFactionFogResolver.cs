using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// SRP: Resolve RT fog phe local cho minimap overlay (registry hoặc FactionFogPresentation active).
    /// </summary>
    public static class MinimapFactionFogResolver
    {
        /// <summary>
        /// Mục tiêu: Client P2 — registry có thể chưa kịp đăng ký; fallback presentation đang active.
        /// Cách hoạt động: LocalOwner → registry; miss thì FactionFogPresentation cùng owner + Register lại.
        /// </summary>
        public static bool TryResolveLocalFactionFog(
            out FactionFogSystemReference factionFog,
            out Camera fogUvCamera)
        {
            if (!TryResolveLocalOwner(out Owner localOwner))
            {
                factionFog = null;
                fogUvCamera = null;
                return false;
            }

            return TryResolveFactionFog(localOwner, out factionFog, out fogUvCamera);
        }

        /// <summary>
        /// Mục tiêu: Minimap HUD P1/P2 — mỗi instance biết phe fog cần sample, không phụ thuộc timing LocalOwner.
        /// Cách hoạt động: Owner serialize/rig → registry; miss thì FactionFogPresentation cùng owner.
        /// </summary>
        public static bool TryResolveFactionFog(
            Owner factionOwner,
            out FactionFogSystemReference factionFog,
            out Camera fogUvCamera)
        {
            factionFog = null;
            fogUvCamera = null;

            if (!HumanFogVisionUtility.EmitsFogVision(factionOwner))
            {
                return false;
            }

            if (FactionFogSystemsRegistry.TryGet(factionOwner, out IFogMapQuery query)
                && query is FactionFogSystemReference registryFog)
            {
                return Finalize(registryFog, out factionFog, out fogUvCamera);
            }

            FactionFogPresentation presentation = FindActivePresentation(factionOwner);
            if (presentation?.FogSystemReference == null)
            {
                return false;
            }

            FactionFogSystemReference fogRef = presentation.FogSystemReference;
            fogRef.EnsureReferences();
            FactionFogSystemsRegistry.Register(fogRef);
            return Finalize(fogRef, out factionFog, out fogUvCamera);
        }

        static bool TryResolveLocalOwner(out Owner localOwner)
        {
            localOwner = Owner.Invalid;
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service != null && service.IsInitialized)
            {
                localOwner = service.LocalOwner;
                return HumanFogVisionUtility.EmitsFogVision(localOwner);
            }

            if (NetworkClient.active)
            {
                return false;
            }

            localOwner = Owner.Player1;
            return true;
        }

        static FactionFogPresentation FindActivePresentation(Owner owner)
        {
            FactionFogPresentation[] presentations = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation == null || presentation.PresentationOwner != owner)
                {
                    continue;
                }

                if (!presentation.isActiveAndEnabled || !presentation.gameObject.activeInHierarchy)
                {
                    continue;
                }

                return presentation;
            }

            return null;
        }

        static bool Finalize(
            FactionFogSystemReference fog,
            out FactionFogSystemReference factionFog,
            out Camera fogUvCamera)
        {
            fog.EnsureReferences();
            factionFog = fog;
            fogUvCamera = fog.ExploredFogCamera ?? fog.VisionFogCamera;
            return fogUvCamera != null
                   && fog.ExploredRenderTexture != null
                   && fog.VisionRenderTexture != null;
        }
    }
}
