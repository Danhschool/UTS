using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Camera con trên Main Camera — chỉ đổi culling mask theo local owner (layer plane P1/P2), không sync mỗi frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayFogOverlayCamera : MonoBehaviour
    {
        const string OverlayChildName = "Fog of War Rendering Camera";

        [SerializeField] Camera fogOverlayCamera;

        Owner _trackedOwner = Owner.Invalid;

        void Awake()
        {
            ResolveOverlayCamera();
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
            TryApplyTrackedOwner();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
        }

#if UNITY_EDITOR
        void OnValidate() => ResolveOverlayCamera();
#endif

        void OnLocalOwnerChanged(Owner owner) => ApplyLocalOwner(owner);

        void TryApplyTrackedOwner()
        {
            if (_trackedOwner != Owner.Invalid)
            {
                ApplyLocalOwner(_trackedOwner);
                return;
            }

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service != null && service.IsInitialized)
            {
                ApplyLocalOwner(service.LocalOwner);
            }
        }

        /// <summary>
        /// Mục tiêu: Client P1/P2 chỉ vẽ plane fog đúng layer — không LateUpdate, không bật/tắt cả prefab fog.
        /// Cách hoạt động: Đặt cullingMask = 1 &lt;&lt; layer plane theo owner; camera con kế thừa transform từ Main Camera.
        /// </summary>
        public void ApplyLocalOwner(Owner localOwner)
        {
            _trackedOwner = localOwner;

            if (fogOverlayCamera == null)
            {
                ResolveOverlayCamera();
            }

            if (fogOverlayCamera == null || !HumanFogVisionUtility.EmitsFogVision(localOwner))
            {
                return;
            }

            int planeLayer = OwnerFogPlaneLayers.GetLayer(localOwner);
            if (planeLayer < 0)
            {
                fogOverlayCamera.enabled = false;
                return;
            }

            fogOverlayCamera.cullingMask = 1 << planeLayer;
            fogOverlayCamera.enabled = true;
        }

        public void SetOverlayEnabled(bool enabled)
        {
            if (fogOverlayCamera != null)
            {
                fogOverlayCamera.enabled = enabled;
            }
        }

        void ResolveOverlayCamera()
        {
            if (fogOverlayCamera != null)
            {
                return;
            }

            Transform child = transform.Find(OverlayChildName);
            if (child != null)
            {
                fogOverlayCamera = child.GetComponent<Camera>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureOnMainCamera()
        {
            Camera main = Camera.main;
            if (main == null || main.GetComponent<GameplayFogOverlayCamera>() != null)
            {
                return;
            }

            if (main.transform.Find(OverlayChildName) == null)
            {
                return;
            }

            main.gameObject.AddComponent<GameplayFogOverlayCamera>();
        }
    }
}
