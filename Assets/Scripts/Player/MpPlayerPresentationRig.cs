using GameDevTV.RTS.Minimap;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Một bộ presentation MP — fog + HUD + PlayerInput cho P1 hoặc P2 (Game 1 / Game 2).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MpPlayerPresentationRig : MonoBehaviour
    {
        [SerializeField] Owner presentationOwner = Owner.Player1;
        [SerializeField] GameObject rigRoot;
        [SerializeField] FactionFogPresentation fogPresentation;
        [SerializeField] GameObject hudRoot;
        [SerializeField] Supplies suppliesHud;
        [SerializeField] PlayerInput playerInput;
        [SerializeField] MinimapUnitIconsController minimapUnitIcons;
        [SerializeField] MinimapFogSystemReference minimapFog;

        public Owner PresentationOwner => presentationOwner;
        public FactionFogPresentation FogPresentation => fogPresentation;
        public PlayerInput PlayerInput => playerInput;
        public GameObject HudRoot => hudRoot;
        public Supplies SuppliesHud => suppliesHud;

        /// <summary>
        /// Mục tiêu: HUD scene thường không parent dưới Fog — rig giữ reference hudRoot/suppliesHud.
        /// </summary>
        public bool OwnsHudTransform(Transform hudTransform)
        {
            if (hudTransform == null)
            {
                return false;
            }

            if (hudRoot != null
                && (hudTransform == hudRoot.transform || hudTransform.IsChildOf(hudRoot.transform)))
            {
                return true;
            }

            if (suppliesHud != null
                && (hudTransform == suppliesHud.transform || hudTransform.IsChildOf(suppliesHud.transform)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Game 1/2 — rig P2 hay thiếu hudRoot/suppliesHud sau merge scene (HUD không parent dưới Fog).
        /// Cách hoạt động: Tìm Supplies theo presentationOwner rồi gán hudRoot = root của HUD đó.
        /// </summary>
        public void TryResolveHudReferencesFromScene()
        {
            suppliesHud ??= MpHudSuppliesResolver.FindForOwner(presentationOwner);

            if (hudRoot == null && suppliesHud != null)
            {
                hudRoot = suppliesHud.transform.root.gameObject;
            }

            if (hudRoot != null)
            {
                suppliesHud ??= hudRoot.GetComponentInChildren<Supplies>(true);
            }
        }

        /// <summary>
        /// Mục tiêu: Director bind — resolve Supplies khi SerializeField trống sau merge scene.
        /// </summary>
        public Supplies ResolveSuppliesHud()
        {
            if (suppliesHud != null)
            {
                return suppliesHud;
            }

            if (hudRoot != null)
            {
                suppliesHud = hudRoot.GetComponentInChildren<Supplies>(true);
            }

            suppliesHud ??= GetComponentInChildren<Supplies>(true);
            return suppliesHud;
        }

        void Awake()
        {
            if (rigRoot == null)
            {
                rigRoot = gameObject;
            }

            TryResolveHudReferencesFromScene();
            fogPresentation ??= GetComponentInChildren<FactionFogPresentation>(true);
            playerInput ??= GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = GetComponentInChildren<PlayerInput>(true);
            }

            if (hudRoot == null)
            {
                Transform hud = transform.Find("HUD");
                if (hud != null)
                {
                    hudRoot = hud.gameObject;
                }
            }

            suppliesHud ??= hudRoot != null
                ? hudRoot.GetComponentInChildren<Supplies>(true)
                : GetComponentInChildren<Supplies>(true);

            if (hudRoot != null)
            {
                minimapUnitIcons ??= hudRoot.GetComponentInChildren<MinimapUnitIconsController>(true);
                ResolveMinimapFogFromHud();
            }
        }

        /// <summary>
        /// Mục tiêu: Scene MP — minimapFog trên rig thường null; mỗi HUD cần bridge fog riêng (không dùng chung scene root).
        /// Cách hoạt động: Lấy MinimapFogSystemReference từ MinimapController trên cùng HUD.
        /// </summary>
        void ResolveMinimapFogFromHud()
        {
            if (minimapFog != null || hudRoot == null)
            {
                return;
            }

            MinimapController minimapController = hudRoot.GetComponentInChildren<MinimapController>(true);
            if (minimapController != null)
            {
                minimapController.RefreshFogPresentation();
                minimapFog = minimapController.FogSystemReference;
                return;
            }

            minimapFog = hudRoot.GetComponentInChildren<MinimapFogSystemReference>(true);
        }

        public MinimapUnitIconsController MinimapUnitIcons => minimapUnitIcons;
        public MinimapFogSystemReference MinimapFog => minimapFog;

        /// <summary>
        /// Mục tiêu: Director bind fog sau khi HUD active — rig scene hay để minimapFog = null.
        /// </summary>
        public void EnsureMinimapFogResolved() => ResolveMinimapFogFromHud();

        /// <summary>
        /// Mục tiêu: P2 rig scene thường thiếu reference PlayerInput — dùng Main Camera chung đã anchor.
        /// </summary>
        public void BindSharedPlayerInput(PlayerInput input)
        {
            playerInput = input;
        }

        /// <summary>
        /// Mục tiêu: Bật fog/HUD/input của phe human trên máy này.
        /// Cách hoạt động: Active rig root + fog presentation + HUD; không tắt camera gameplay chung.
        /// </summary>
        public void SetRigActive(bool active)
        {
            if (rigRoot != null)
            {
                rigRoot.SetActive(active);
            }

            fogPresentation?.SetPresentationActive(active);

            if (hudRoot != null)
            {
                hudRoot.SetActive(active);
            }

            if (active)
            {
                Supplies hud = ResolveSuppliesHud();
                if (hud != null)
                {
                    if (!hud.gameObject.activeInHierarchy)
                    {
                        hud.gameObject.SetActive(true);
                    }

                    hud.BindHudOwner(presentationOwner);
                }

                WireRuntimeUiBusOwner();
                EnsureMinimapFogResolved();
                BindMinimapFactionOwner();
            }
            else
            {
                UnwireRuntimeUiBus();
            }

            FactionVisibilityUpdater[] visibilityUpdaters =
                GetComponentsInChildren<FactionVisibilityUpdater>(true);
            for (int i = 0; i < visibilityUpdaters.Length; i++)
            {
                FactionVisibilityUpdater updater = visibilityUpdaters[i];
                if (updater != null)
                {
                    updater.enabled = active;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Minimap overlay/icon dùng RT fog đúng phe (P1 rig → Player1, P2 rig → Player2).
        /// </summary>
        void BindMinimapFactionOwner()
        {
            if (hudRoot == null)
            {
                return;
            }

            MinimapController minimap = hudRoot.GetComponentInChildren<MinimapController>(true);
            minimap?.BindFactionOwner(presentationOwner);
            minimapUnitIcons?.BindLocalOwner(presentationOwner);
        }

        /// <summary>
        /// Mục tiêu: HUD P2 RuntimeUI nghe Bus Player2 (không dùng Player1 mặc định).
        /// </summary>
        void WireRuntimeUiBusOwner()
        {
            if (hudRoot == null)
            {
                return;
            }

            RuntimeUI[] runtimeUis = hudRoot.GetComponentsInChildren<RuntimeUI>(true);
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                RuntimeUI ui = runtimeUis[i];
                if (ui == null || ui.IsConfiguredForOwner(presentationOwner))
                {
                    continue;
                }

                ui.ConfigureBusOwner(presentationOwner);
            }
        }

        /// <summary>Mục tiêu: Rig inactive — gỡ Bus selection UI khỏi phe này.</summary>
        void UnwireRuntimeUiBus()
        {
            if (hudRoot == null)
            {
                return;
            }

            RuntimeUI[] runtimeUis = hudRoot.GetComponentsInChildren<RuntimeUI>(true);
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                runtimeUis[i].ReleaseBusSubscription();
            }
        }

        /// <summary>Mục tiêu: Director Apply — đồng bộ RuntimeUI sau bind rig.</summary>
        public void RefreshRuntimeUiForOwner(Owner localOwner)
        {
            if (!HumanFogVisionUtility.IsHumanPlayer(localOwner) || presentationOwner != localOwner)
            {
                return;
            }

            WireRuntimeUiBusOwner();
            MpRuntimeUiCoordinator.RefreshFullPresentationForOwner(localOwner);
        }

        public void SetPlayerInputEnabled(bool enabled)
        {
            if (playerInput != null)
            {
                playerInput.enabled = enabled;
            }
        }
    }
}
