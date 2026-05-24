using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Orchestrator — bật đúng nhánh fog presentation theo <see cref="ILocalHumanOwner.LocalOwner"/>.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class PlayerViewBinder : MonoBehaviour
    {
        [SerializeField] LocalHumanOwnerService localHumanOwnerService;
        [SerializeField] FactionFogPresentation presentationPlayer1;
        [SerializeField] FactionFogPresentation presentationPlayer2;
        [SerializeField] FactionVisibilityUpdater visibilityUpdater;
        [SerializeField] FactionHudBinder hudBinder;

        void Awake()
        {
            if (localHumanOwnerService == null)
            {
                localHumanOwnerService = LocalHumanOwnerService.EnsureExists();
            }

            ResolveSceneReferences();

            presentationPlayer1?.SetPresentationActive(false);
            presentationPlayer2?.SetPresentationActive(false);
        }

        /// <summary>
        /// Mục tiêu: Scene Game 1 không bắt buộc kéo thủ công mọi reference fog.
        /// Cách hoạt động: Tìm presentation theo <see cref="FactionFogPresentation.PresentationOwner"/> và visibility updater trong scene.
        /// </summary>
        void ResolveSceneReferences()
        {
            if (visibilityUpdater == null)
            {
                visibilityUpdater = FindFirstObjectByType<FactionVisibilityUpdater>(FindObjectsInactive.Include);
            }

            if (presentationPlayer1 != null && presentationPlayer2 != null)
            {
                return;
            }

            FactionFogPresentation[] presentations = FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation == null)
                {
                    continue;
                }

                if (presentation.PresentationOwner == Owner.Player1 && presentationPlayer1 == null)
                {
                    presentationPlayer1 = presentation;
                }
                else if (presentation.PresentationOwner == Owner.Player2 && presentationPlayer2 == null)
                {
                    presentationPlayer2 = presentation;
                }
            }

            if (presentationPlayer1 == null && presentations.Length == 1)
            {
                presentationPlayer1 = presentations[0];
            }
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
        /// Mục tiêu: Scene MP load xong — refresh fog/HUD theo LocalOwner hiện tại.
        /// </summary>
        public void RefreshFromLocalOwner() => TryApply();

        /// <summary>
        /// Mục tiêu: Client chỉ render fog/UI nhánh của human đang ngồi máy.
        /// Cách hoạt động: Enable P1 hoặc P2 presentation; bind vision camera cho updater.
        /// </summary>
        public void Apply(Owner localOwner)
        {
            if (!HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            bool usePlayer1 = localOwner == Owner.Player1;
            presentationPlayer1?.SetPresentationActive(usePlayer1);
            presentationPlayer2?.SetPresentationActive(!usePlayer1);

            FactionFogPresentation active = usePlayer1 ? presentationPlayer1 : presentationPlayer2;
            if (active == null)
            {
                active = presentationPlayer1 ?? presentationPlayer2;
            }

            if (visibilityUpdater != null && active != null)
            {
                visibilityUpdater.BindVisionCamera(active.VisionFogCamera);
                visibilityUpdater.BindLocalOwner(localOwner);
            }

            if (hudBinder == null)
            {
                hudBinder = FindFirstObjectByType<FactionHudBinder>(FindObjectsInactive.Include);
            }

            hudBinder?.Apply(localOwner);
        }
    }
}
