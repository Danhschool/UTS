using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Gắn HUD / minimap / event log theo <see cref="ILocalHumanOwner.LocalOwner"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FactionHudBinder : MonoBehaviour
    {
        [SerializeField] LocalHumanOwnerService localHumanOwnerService;
        [SerializeField] Supplies supplies;
        [SerializeField] PlayerGameEventLogListener gameEventLogListener;
        [SerializeField] MinimapUnitIconsController minimapUnitIcons;
        [SerializeField] MinimapFogSystemReference minimapFog;

        void Awake()
        {
            if (localHumanOwnerService == null)
            {
                localHumanOwnerService = LocalHumanOwnerService.EnsureExists();
            }

            ResolveSceneReferences();
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += ApplyFromService;
            TryApply();
        }

        void Start()
        {
            TryApply();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= ApplyFromService;
        }

        void ResolveSceneReferences()
        {
            supplies ??= FindFirstObjectByType<Supplies>(FindObjectsInactive.Include);
            gameEventLogListener ??= FindFirstObjectByType<PlayerGameEventLogListener>(FindObjectsInactive.Include);
            minimapUnitIcons ??= FindFirstObjectByType<MinimapUnitIconsController>(FindObjectsInactive.Include);
            minimapFog ??= FindFirstObjectByType<MinimapFogSystemReference>(FindObjectsInactive.Include);
        }

        void ApplyFromService(Owner owner) => Apply(owner);

        void TryApply()
        {
            if (localHumanOwnerService == null)
            {
                localHumanOwnerService = LocalHumanOwnerService.Instance;
            }

            if (localHumanOwnerService == null || !localHumanOwnerService.IsInitialized)
            {
                return;
            }

            Apply(localHumanOwnerService.LocalOwner);
        }

        /// <summary>
        /// Mục tiêu: Client chỉ thấy tài nguyên, log sự kiện và minimap của phe mình.
        /// Cách hoạt động: Bind Supplies + event log + minimap filter; fog minimap từ registry nhánh active.
        /// </summary>
        public void Apply(Owner localOwner, Supplies ownerSupplies = null)
        {
            Apply(localOwner, ownerSupplies, minimapUnitIcons, minimapFog);
        }

        /// <summary>
        /// Mục tiêu: HUD/minimap đúng nhánh P1 hoặc P2 (không FindFirstObjectByType luôn lấy P1).
        /// </summary>
        public void Apply(
            Owner localOwner,
            Supplies ownerSupplies,
            MinimapUnitIconsController rigMinimapIcons,
            MinimapFogSystemReference rigMinimapFog)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            Supplies hud = ownerSupplies ?? MpHudSuppliesResolver.FindForOwner(localOwner) ?? supplies;
            if (hud != null)
            {
                supplies = hud;
                hud.gameObject.SetActive(true);
                hud.BindHudOwner(localOwner);
            }

            gameEventLogListener?.SetListenOwner(localOwner);

            MinimapUnitIconsController icons = rigMinimapIcons ?? minimapUnitIcons;
            if (icons != null)
            {
                minimapUnitIcons = icons;
                icons.BindLocalOwner(localOwner);
            }

            MinimapFogSystemReference fogRef = rigMinimapFog ?? minimapFog;
            if (fogRef != null)
            {
                minimapFog = fogRef;
            }

            BindMinimapFog(localOwner);
        }

        void BindMinimapFog(Owner localOwner)
        {
            if (minimapFog == null)
            {
                return;
            }

            if (!FactionFogSystemsRegistry.TryGet(localOwner, out IFogMapQuery query)
                || query is not FactionFogSystemReference factionRef)
            {
                return;
            }

            factionRef.EnsureReferences();
            minimapFog.BindFromFactionFog(factionRef);
        }
    }
}
