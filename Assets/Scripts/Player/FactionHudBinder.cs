using GameDevTV.RTS.Audio;
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
        [SerializeField] PlayerAudioListener playerAudioListener;
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
            playerAudioListener ??= GetComponent<PlayerAudioListener>();
            if (playerAudioListener == null)
            {
                playerAudioListener = gameObject.AddComponent<PlayerAudioListener>();
            }

            minimapUnitIcons ??= FindFirstObjectByType<MinimapUnitIconsController>(FindObjectsInactive.Include);

            if (FindFirstObjectByType<AudioBootstrap>(FindObjectsInactive.Include) == null)
            {
                AudioBootstrap.EnsureExists();
            }
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

            if (MpPresentationDirectorGate.ShouldDeferHudAndFogToDirector())
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

            if (gameEventLogListener != null)
            {
                gameEventLogListener.SetListenOwner(localOwner);
            }

            PlayerAudioListener audioListener = ResolvePlayerAudioListener();
            if (audioListener != null)
            {
                audioListener.SetListenOwner(localOwner);
            }

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

        /// <summary>
        /// Mục tiêu: Client P2 — registry fog có thể đăng ký sau Apply đầu; minimap cần rebind mỗi khi fog active.
        /// Cách hoạt động: Gán rig minimap fog rồi gọi BindMinimapFog từ FactionFogSystemsRegistry.
        /// </summary>
        public void RebindMinimapFog(Owner localOwner, MinimapFogSystemReference rigMinimapFog = null)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            MinimapFogSystemReference fogRef = rigMinimapFog ?? minimapFog;
            if (fogRef != null)
            {
                minimapFog = fogRef;
            }

            BindMinimapFog(localOwner);
        }

        /// <summary>
        /// Mục tiêu: Tránh MissingReferenceException — C# ?. không dùng Unity fake-null.
        /// </summary>
        PlayerAudioListener ResolvePlayerAudioListener()
        {
            if (playerAudioListener == null)
            {
                playerAudioListener = GetComponent<PlayerAudioListener>();
            }

            if (playerAudioListener == null && gameObject != null)
            {
                playerAudioListener = gameObject.AddComponent<PlayerAudioListener>();
            }

            return playerAudioListener;
        }

        void BindMinimapFog(Owner localOwner)
        {
            if (!FactionFogSystemsRegistry.TryGet(localOwner, out IFogMapQuery query)
                || query is not FactionFogSystemReference factionRef)
            {
                return;
            }

            factionRef.EnsureReferences();

            if (minimapFog != null)
            {
                minimapFog.BindFromFactionFog(factionRef);
                RefreshMinimapControllerForFog(minimapFog);
            }

            BindActiveMinimapControllers(localOwner, factionRef);
        }

        /// <summary>
        /// Mục tiêu: MP — mọi MinimapController HUD đang active phải bind RT P1/P2 (không chỉ Minimap Fog Bridge scene).
        /// Cách hoạt động: Find MinimapController active → BindFromFactionFog + RefreshFogPresentation.
        /// </summary>
        static void BindActiveMinimapControllers(Owner localOwner, FactionFogSystemReference factionRef)
        {
            MinimapController[] controllers = Object.FindObjectsByType<MinimapController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                MinimapController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                bool isActiveHud = controller.isActiveAndEnabled && controller.gameObject.activeInHierarchy;
                if (!isActiveHud)
                {
                    controller.SetExploredFogOverlayEnabled(false);
                    continue;
                }

                controller.BindFactionOwner(localOwner);
                controller.RefreshFogPresentation();
                MinimapFogSystemReference fog = controller.FogSystemReference;
                if (fog == null)
                {
                    continue;
                }

                fog.BindFromFactionFog(factionRef);
                controller.RefreshFogPresentation();
            }
        }

        static void RefreshMinimapControllerForFog(MinimapFogSystemReference fogRef)
        {
            if (fogRef == null)
            {
                return;
            }

            MinimapController controller = fogRef.GetComponent<MinimapController>();
            if (controller == null)
            {
                controller = fogRef.GetComponentInParent<MinimapController>(true);
            }

            controller?.RefreshFogPresentation();
        }
    }
}
