using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;
namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: MP — mỗi client chỉ bật rig P1 hoặc P2 (fog + UI + PlayerInput riêng).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class MpPlayerPresentationDirector : MonoBehaviour
    {
        [SerializeField] MpPlayerPresentationRig player1Rig;
        [SerializeField] MpPlayerPresentationRig player2Rig;
        [SerializeField] FactionVisibilityUpdater visibilityUpdater;
        [SerializeField] FactionHudBinder hudBinder;
        [SerializeField] Camera sharedGameplayCamera;
        [SerializeField] Transform gameplayCameraAnchor;

        PlayerInput _sharedPlayerInput;
        Owner _lastAppliedOwner = Owner.Invalid;
        int _lastP2UnitsSynced = -1;
        bool _inactiveFogPresentationsSuppressed;

        public bool HasConfiguredRigs => player1Rig != null || player2Rig != null;

        void Awake()
        {
            DeactivateAllRigs();
            AnchorSharedGameplayCamera();
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
            TryApply();
        }

        void Start() => TryApply();

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
        }

        public void RefreshFromLocalOwner() => TryApply();

        void OnLocalOwnerChanged(Owner owner) => Apply(owner);

        void TryApply()
        {
            if (!MpLocalOwnerSceneSync.EnsureLocalOwnerInitialized(out Owner localOwner))
            {
                return;
            }

            Apply(localOwner);
        }

        /// <summary>
        /// Mục tiêu: Host = P1, Client = P2 — mỗi người một fog, HUD, input.
        /// Cách hoạt động: Bật một rig; bind vision camera; FactionHudBinder cho minimap/supplies.
        /// </summary>
        public void Apply(Owner localOwner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            bool ownerChanged = localOwner != _lastAppliedOwner;
            if (!MpFogRefreshThrottle.ShouldRunPresentationApply(ownerChanged))
            {
                TryRefreshP2VisionLayersOnly(localOwner);
                return;
            }

            _lastAppliedOwner = localOwner;

            bool usePlayer1 = localOwner == Owner.Player1;

            if (player2Rig == null && localOwner == Owner.Player2)
            {
                Debug.LogError(
                    "[MpPlayerPresentationDirector] player2Rig chưa gán — chạy ProjectRTS/Netplay/★ Prepare RtsNet_Game Scene.");
            }

            if (ownerChanged || !_inactiveFogPresentationsSuppressed)
            {
                SuppressInactiveFogPresentations(localOwner);
                _inactiveFogPresentationsSuppressed = true;
            }

            AnchorSharedGameplayCamera();

            player1Rig?.SetRigActive(usePlayer1);
            player2Rig?.SetRigActive(!usePlayer1);

            MpPlayerPresentationRig activeRig = usePlayer1 ? player1Rig : player2Rig;
            ApplyPlayerInput(activeRig, localOwner);
            EnsureSharedCameraEnabled();

            FactionFogPresentation activeFog = activeRig?.FogPresentation;
            if (activeFog != null)
            {
                activeFog.SetPresentationActive(true);
                activeFog.FogSystemReference?.EnsureReferences();
                activeFog.ApplyOwnerCameraMasks();
                activeFog.RefreshFogTextures();

                FactionVisibilityUpdater updater =
                    activeFog.GetComponentInChildren<FactionVisibilityUpdater>(true);

                if (updater != null)
                {
                    visibilityUpdater = updater;
                    updater.BindVisionCamera(activeFog.VisionFogCamera);
                    updater.BindLocalOwner(localOwner);
                }
            }

            if (localOwner == Owner.Player2)
            {
                int p2Units = RefreshP2UnitFogVisionLayers();
                if (p2Units > _lastP2UnitsSynced)
                {
                    _lastP2UnitsSynced = p2Units;
                    visibilityUpdater?.RefreshVisibilityAfterVisionLayers();
                }
            }

            if (ownerChanged)
            {
                hudBinder ??= FindFirstObjectByType<FactionHudBinder>(FindObjectsInactive.Include);
                hudBinder?.Apply(
                    localOwner,
                    activeRig?.SuppliesHud,
                    activeRig?.MinimapUnitIcons,
                    activeRig?.MinimapFog);

                ApplyGameplayFogOverlayForOwner(localOwner);

                if (localOwner == Owner.Player2)
                {
                    ValidatePlayer2PresentationSetup(activeRig, activeFog);
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Apply bị throttle — vẫn sync layer vision khi unit P2 spawn thêm.
        /// </summary>
        void TryRefreshP2VisionLayersOnly(Owner localOwner)
        {
            if (localOwner != Owner.Player2)
            {
                return;
            }

            int p2Units = RefreshP2UnitFogVisionLayers();
            if (p2Units <= _lastP2UnitsSynced)
            {
                return;
            }

            _lastP2UnitsSynced = p2Units;
            visibilityUpdater?.RefreshVisibilityAfterVisionLayers();
        }

        /// <summary>
        /// Mục tiêu: Client P2 — unit replicate trước presentation; gán layer 14 + chỉ local P2 bật Vision.
        /// </summary>
        int RefreshP2UnitFogVisionLayers()
        {
            int p2Units = 0;

            AbstractCommandable[] commandables = Object.FindObjectsByType<AbstractCommandable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < commandables.Length; i++)
            {
                AbstractCommandable commandable = commandables[i];
                if (commandable == null || commandable.Owner != Owner.Player2)
                {
                    continue;
                }

                commandable.SyncOwnerAndFogVision(Owner.Player2);
                p2Units++;
            }

            return p2Units;
        }

        /// <summary>
        /// Mục tiêu: Client P2 — báo lỗi cấu hình rig/RT/mask khi setup scene sai.
        /// </summary>
        void ValidatePlayer2PresentationSetup(MpPlayerPresentationRig activeRig, FactionFogPresentation activeFog)
        {
            FactionFogSystemReference fogRef = activeFog?.FogSystemReference;
            fogRef?.EnsureReferences();

            Camera visionCam = activeFog?.VisionFogCamera;
            int expectedVisionMask = 1 << OwnerFogVisionLayers.GetLayer(Owner.Player2);
            int expectedOverlayMask = 1 << OwnerFogPlaneLayers.GetLayer(Owner.Player2);
            int visionMask = visionCam != null ? visionCam.cullingMask : -1;
            string visionRtName = fogRef?.VisionRenderTexture != null ? fogRef.VisionRenderTexture.name : "null";
            string exploredRtName = fogRef?.ExploredRenderTexture != null ? fogRef.ExploredRenderTexture.name : "null";
            bool rtNamesOk = visionRtName.Contains("P2") && exploredRtName.Contains("P2");
            bool visionMaskOk = visionMask == expectedVisionMask;

            Camera overlayCam = sharedGameplayCamera != null
                ? sharedGameplayCamera.transform.Find("Fog of War Rendering Camera")?.GetComponent<Camera>()
                : null;
            int overlayMask = overlayCam != null ? overlayCam.cullingMask : -1;
            bool overlayMaskOk = overlayMask == expectedOverlayMask;

            if (player2Rig == null)
            {
                Debug.LogError("[P2 Fog] player2Rig NULL — chạy Prepare RtsNet_Game Scene.");
            }
            else if (activeRig != player2Rig)
            {
                Debug.LogError($"[P2 Fog] activeRig sai: {activeRig?.name} (cần player2Rig).");
            }
            else if (!rtNamesOk)
            {
                Debug.LogError($"[P2 Fog] RT sai: vision={visionRtName}, explored={exploredRtName} (cần *P2*).");
            }
            else if (!visionMaskOk)
            {
                Debug.LogError($"[P2 Fog] Vision mask={visionMask}, cần {expectedVisionMask} (layer Fog of War Vision / 14).");
            }
            else if (!overlayMaskOk)
            {
                Debug.LogError($"[P2 Fog] Overlay mask={overlayMask}, cần {expectedOverlayMask} (layer Fog of War Plane P2).");
            }
        }

        void ApplyGameplayFogOverlayForOwner(Owner localOwner)
        {
            Camera rigCamera = _sharedPlayerInput != null ? _sharedPlayerInput.GameplayCamera : null;
            if (rigCamera != null)
            {
                sharedGameplayCamera = rigCamera;
            }
            else if (sharedGameplayCamera == null && player1Rig?.PlayerInput != null)
            {
                sharedGameplayCamera = player1Rig.PlayerInput.GameplayCamera;
            }

            if (sharedGameplayCamera == null)
            {
                sharedGameplayCamera = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include)?.GameplayCamera;
            }

            if (sharedGameplayCamera == null)
            {
                return;
            }

            GameplayFogOverlayCamera overlay = sharedGameplayCamera.GetComponent<GameplayFogOverlayCamera>();
            if (overlay == null)
            {
                overlay = sharedGameplayCamera.gameObject.AddComponent<GameplayFogOverlayCamera>();
            }

            overlay.ApplyLocalOwner(localOwner);
        }

        /// <summary>
        /// Mục tiêu: Client P2 không bị plane fog P1 che màn hình đen.
        /// Cách hoạt động: Tắt mọi FactionFogPresentation không thuộc localOwner.
        /// </summary>
        static void SuppressInactiveFogPresentations(Owner localOwner)
        {
            FactionFogPresentation[] presentations = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation == null)
                {
                    continue;
                }

                bool isActiveBranch = presentation.PresentationOwner == localOwner;
                presentation.SetPresentationActive(isActiveBranch);
            }
        }

        void DeactivateAllRigs()
        {
            player1Rig?.SetRigActive(false);
            player2Rig?.SetRigActive(false);
            player1Rig?.SetPlayerInputEnabled(false);
            player2Rig?.SetPlayerInputEnabled(false);
        }

        /// <summary>
        /// Mục tiêu: Main Camera không nằm dưới Fog P1 inactive — P2 vẫn có input + Cinemachine.
        /// Cách hoạt động: Reparent PlayerInput lên anchor Director; gán vào cả hai rig.
        /// </summary>
        void AnchorSharedGameplayCamera()
        {
            ResolveSharedPlayerInput();
            if (_sharedPlayerInput == null)
            {
                return;
            }

            Transform anchor = gameplayCameraAnchor != null ? gameplayCameraAnchor : transform;
            Transform inputTransform = _sharedPlayerInput.transform;
            if (inputTransform.parent != anchor)
            {
                inputTransform.SetParent(anchor, true);
            }

            if (!inputTransform.gameObject.activeSelf)
            {
                inputTransform.gameObject.SetActive(true);
            }

            player1Rig?.BindSharedPlayerInput(_sharedPlayerInput);
            player2Rig?.BindSharedPlayerInput(_sharedPlayerInput);
        }

        void ApplyPlayerInput(MpPlayerPresentationRig activeRig, Owner localOwner)
        {
            ResolveSharedPlayerInput();
            PlayerInput activeInput = _sharedPlayerInput;

            player1Rig?.SetPlayerInputEnabled(false);
            player2Rig?.SetPlayerInputEnabled(false);

            if (activeInput == null)
            {
                return;
            }

            activeInput.enabled = true;
        }

        void EnsureSharedCameraEnabled()
        {
            ResolveSharedPlayerInput();
            if (_sharedPlayerInput != null)
            {
                sharedGameplayCamera = _sharedPlayerInput.GameplayCamera;
            }

            if (sharedGameplayCamera == null)
            {
                return;
            }

            if (!sharedGameplayCamera.gameObject.activeInHierarchy)
            {
                sharedGameplayCamera.gameObject.SetActive(true);
            }

            sharedGameplayCamera.enabled = true;
        }

        void ResolveSharedPlayerInput()
        {
            if (_sharedPlayerInput != null)
            {
                return;
            }

            _sharedPlayerInput = player1Rig?.PlayerInput;
            if (_sharedPlayerInput == null)
            {
                _sharedPlayerInput = FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
            }
        }
    }
}
