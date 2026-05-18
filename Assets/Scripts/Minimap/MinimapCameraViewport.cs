using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapCameraViewport : MonoBehaviour
    {
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private Image viewportImage;
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MonoBehaviour cameraNavigatorBehaviour;
        [SerializeField] private float groundHeight;
        [SerializeField] private bool autoFindPlayerInput = true;
        [SerializeField] private Color viewportFrameColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Sprite viewportFrameSprite;

        private static Sprite runtimeFrameSprite;
        private IMinimapCameraNavigator cameraNavigator;
        private bool warnedMissingNavigator;

        private void Awake()
        {
            if (viewportRect == null)
            {
                viewportRect = transform as RectTransform;
            }

            EnsureViewportVisual();
            ResolveNavigator();
        }

        public void SetCameraNavigator(MonoBehaviour navigatorBehaviour)
        {
            cameraNavigatorBehaviour = navigatorBehaviour;
            ResolveNavigator();
        }

        private void ResolveNavigator()
        {
            cameraNavigator = cameraNavigatorBehaviour as IMinimapCameraNavigator;
            if (cameraNavigator == null && cameraNavigatorBehaviour != null)
            {
                cameraNavigatorBehaviour = null;
            }

            if (cameraNavigator == null && autoFindPlayerInput)
            {
                cameraNavigatorBehaviour = FindFirstObjectByType<PlayerInput>();
                cameraNavigator = cameraNavigatorBehaviour as IMinimapCameraNavigator;
            }
        }

        private void EnsureViewportVisual()
        {
            if (viewportImage == null)
            {
                return;
            }

            if (viewportImage.sprite == null)
            {
                viewportImage.sprite = viewportFrameSprite != null
                    ? viewportFrameSprite
                    : GetOrCreateRuntimeFrameSprite();
                viewportImage.type = Image.Type.Simple;
            }

            viewportImage.color = viewportFrameColor;
            viewportImage.raycastTarget = false;
        }

        /// <summary>
        /// Mục tiêu: Có sprite trắng để Image viewport hiển thị khung trên minimap.
        /// Cách hoạt động: Tạo texture 4x4 một lần và cache Sprite (Unity 6 không còn UISprite.psd built-in).
        /// </summary>
        private static Sprite GetOrCreateRuntimeFrameSprite()
        {
            if (runtimeFrameSprite != null)
            {
                return runtimeFrameSprite;
            }

            const int size = 4;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            runtimeFrameSprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeFrameSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeFrameSprite;
        }

        private void LateUpdate()
        {
            if (mapBounds == null || viewportRect == null)
            {
                return;
            }

            if (cameraNavigator == null)
            {
                if (!warnedMissingNavigator)
                {
                    Debug.LogWarning(
                        "MinimapCameraViewport: chưa gán Camera Navigator (Main Camera + Player Input). Khung viewport sẽ không cập nhật.",
                        this);
                    warnedMissingNavigator = true;
                }

                return;
            }

            Transform target = cameraNavigator.CameraTargetTransform;
            Camera gameplayCamera = cameraNavigator.GameplayCamera;
            if (target == null || gameplayCamera == null)
            {
                viewportRect.gameObject.SetActive(false);
                return;
            }

            viewportRect.gameObject.SetActive(true);
            float planeY = GetGroundPlaneY(target);
            if (!TryGetViewBoundsOnGround(target, gameplayCamera, planeY, out float minX, out float maxX, out float minZ, out float maxZ))
            {
                return;
            }

            Vector2 minUv = mapBounds.WorldToNormalized(new Vector3(minX, planeY, minZ));
            Vector2 maxUv = mapBounds.WorldToNormalized(new Vector3(maxX, planeY, maxZ));
            viewportRect.anchorMin = new Vector2(Mathf.Min(minUv.x, maxUv.x), Mathf.Min(minUv.y, maxUv.y));
            viewportRect.anchorMax = new Vector2(Mathf.Max(minUv.x, maxUv.x), Mathf.Max(minUv.y, maxUv.y));
            viewportRect.anchoredPosition = Vector2.zero;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.localRotation = Quaternion.identity;
            viewportRect.localScale = Vector3.one;

            if (viewportImage != null)
            {
                viewportImage.enabled = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Tính vùng nhìn camera trên mặt phẳng XZ để vẽ khung minimap.
        /// Cách hoạt động: Ortho dùng orthographicSize; perspective bắn 4 tia góc viewport giao mặt phẳng Y cố định.
        /// </summary>
        private float GetGroundPlaneY(Transform cameraTarget)
        {
            if (Mathf.Abs(groundHeight) > 0.001f)
            {
                return groundHeight;
            }

            return cameraTarget != null ? cameraTarget.position.y : 0f;
        }

        private bool TryGetViewBoundsOnGround(Transform cameraTarget, Camera cam, float planeY, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                minX = cameraTarget.position.x - halfWidth;
                maxX = cameraTarget.position.x + halfWidth;
                minZ = cameraTarget.position.z - halfHeight;
                maxZ = cameraTarget.position.z + halfHeight;
                return true;
            }

            Plane ground = new(Vector3.up, new Vector3(0f, planeY, 0f));
            minX = maxX = cameraTarget.position.x;
            minZ = maxZ = cameraTarget.position.z;
            bool anyHit = false;

            for (int i = 0; i < 4; i++)
            {
                Vector2 uv = i switch
                {
                    0 => new Vector2(0f, 0f),
                    1 => new Vector2(1f, 0f),
                    2 => new Vector2(1f, 1f),
                    _ => new Vector2(0f, 1f),
                };

                Ray ray = cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
                if (!ground.Raycast(ray, out float enter) || enter <= 0f)
                {
                    continue;
                }

                Vector3 hit = ray.GetPoint(enter);
                minX = anyHit ? Mathf.Min(minX, hit.x) : hit.x;
                maxX = anyHit ? Mathf.Max(maxX, hit.x) : hit.x;
                minZ = anyHit ? Mathf.Min(minZ, hit.z) : hit.z;
                maxZ = anyHit ? Mathf.Max(maxZ, hit.z) : hit.z;
                anyHit = true;
            }

            if (anyHit)
            {
                return true;
            }

            float distance = Mathf.Max(1f, cam.transform.position.y - planeY);
            float approxHalfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float approxHalfWidth = approxHalfHeight * cam.aspect;
            minX = cameraTarget.position.x - approxHalfWidth;
            maxX = cameraTarget.position.x + approxHalfWidth;
            minZ = cameraTarget.position.z - approxHalfHeight;
            maxZ = cameraTarget.position.z + approxHalfHeight;
            return true;
        }
    }
}
