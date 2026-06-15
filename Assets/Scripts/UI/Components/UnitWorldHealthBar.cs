using System.Collections;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// Thanh máu world-space: fill theo HP và đổi sprite theo <see cref="Owner"/> (Player1 / Player2).
    /// </summary>
    [ExecuteAlways]
    public class UnitWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private AbstractCommandable commandable;
        [SerializeField] private ProgressBar progressBar;
        [SerializeField] private OwnerHealthBarStyleSO styleLibrary;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image fillImage;
        [Tooltip("Để trống sẽ dùng Camera.main một lần trong Awake (nên gán camera gameplay trong Inspector nếu có nhiều camera).")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool billboardTowardCamera = true;

        private Owner lastAppliedOwner = Owner.Invalid;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ApplyOwnerStyleIfNeeded();
        }

        private void Start()
        {
            if (commandable == null || progressBar == null)
            {
                return;
            }

            commandable.OnHealthUpdated += HandleHealthUpdated;
            StartCoroutine(RefreshAfterHealthInitialized());
            ApplyOwnerStyleIfNeeded();
        }

        private void OnDestroy()
        {
            if (commandable != null)
            {
                commandable.OnHealthUpdated -= HandleHealthUpdated;
            }
        }

        private void LateUpdate()
        {
            if (!billboardTowardCamera || worldCamera == null)
            {
                return;
            }

            Vector3 toCamera = transform.position - worldCamera.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ApplyOwnerStyleIfNeeded();
            }
#endif
        }

        /// <summary>
        /// Mục tiêu: Gọi từ <see cref="AbstractCommandable"/> khi đổi Owner trên Inspector (xem trước không cần Play).
        /// Cách hoạt động: Reset cache Owner và áp lại sprite border/fill từ <see cref="OwnerHealthBarStyleSO"/>.
        /// </summary>
        public void RefreshOwnerStyleInEditor()
        {
            lastAppliedOwner = Owner.Invalid;
            ResolveReferences();
            ApplyOwnerStyleIfNeeded();
        }

        private IEnumerator RefreshAfterHealthInitialized()
        {
            yield return null;
            ApplyFillFromHealth();
        }

        private void ResolveReferences()
        {
            if (commandable == null)
            {
                commandable = GetComponentInParent<AbstractCommandable>();
            }

            if (progressBar == null)
            {
                progressBar = GetComponentInChildren<ProgressBar>(true);
            }

            if (progressBar != null && borderImage == null)
            {
                progressBar.TryGetComponent(out borderImage);
            }

            if (fillImage == null && progressBar != null)
            {
                Transform progressTransform = progressBar.transform.Find("Mask/Progress");
                if (progressTransform == null)
                {
                    progressTransform = FindDeepChild(progressBar.transform, "Progress");
                }

                if (progressTransform != null)
                {
                    progressTransform.TryGetComponent(out fillImage);
                }
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void ApplyOwnerStyleIfNeeded()
        {
            if (styleLibrary == null || commandable == null)
            {
                return;
            }

            if (commandable.Owner == lastAppliedOwner)
            {
                return;
            }

            if (!styleLibrary.TryGetStyle(commandable.Owner, out OwnerHealthBarStyleSO.Style style))
            {
                return;
            }

            ApplySpriteToImage(borderImage, style.borderSprite);
            ApplySpriteToImage(fillImage, style.fillSprite);

            lastAppliedOwner = commandable.Owner;
        }

        /// <summary>
        /// Mục tiêu: Gán sprite và chọn Image Type phù hợp (Simple vs Sliced) để tránh cảnh báo "too many sprite tiles".
        /// Cách hoạt động: Nếu sprite có border 9-slice thì dùng Sliced; ngược lại dùng Simple.
        /// </summary>
        private static void ApplySpriteToImage(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            Vector4 border = sprite.border;
            bool hasNineSlice = border.x > 0.01f || border.y > 0.01f || border.z > 0.01f || border.w > 0.01f;
            image.type = hasNineSlice ? Image.Type.Sliced : Image.Type.Simple;
        }

        private void ApplyFillFromHealth()
        {
            if (progressBar == null || commandable == null)
            {
                return;
            }

            float ratio = commandable.MaxHealth > 0
                ? Mathf.Clamp01((float)commandable.CurrentHealth / commandable.MaxHealth)
                : 0f;

            progressBar.SetProgress(ratio);
        }

        private void HandleHealthUpdated(AbstractCommandable _, int __, int ___)
        {
            if (this == null || commandable == null || progressBar == null)
            {
                return;
            }

            ApplyFillFromHealth();
        }
    }
}
