using UnityEngine;

namespace GameDevTV.RTS.Units.Visualization
{
    /// <summary>
    /// Hiển thị vòng tròn tầm đánh khi unit được chọn (LineRenderer) và trong Scene view (Gizmos).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitAttackRangeDisplay : MonoBehaviour
    {
        private const int DefaultSegments = 64;

        [Header("Range source")]
        [SerializeField] private AbstractCommandable commandable;
        [Tooltip("Ghi đè tầm đánh thủ công (≤ 0 = lấy từ UnitSO.AttackConfig).")]
        [SerializeField] private float attackRangeOverride;

        [Header("Ring (runtime)")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private bool showWhenSelected = true;
        [SerializeField] private float groundYOffset = 0.05f;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private Color ringColor = new(1f, 0.35f, 0.2f, 0.85f);

        [Header("Gizmos (Scene view)")]
        [SerializeField] private bool drawGizmosInEditor = true;
        [SerializeField] private bool drawGizmosOnlyWhenSelected = true;
        [SerializeField] private Color gizmoColor = new(1f, 0.4f, 0.15f, 0.9f);
        [SerializeField] private int gizmoSegments = 48;

        private readonly Vector3[] circleBuffer = new Vector3[DefaultSegments];
        private UnitSO unitSo;
        private bool ringVisible;
        private Material runtimeLineMaterial;

        private void Awake()
        {
            commandable ??= GetComponent<AbstractCommandable>();

            if (commandable is BaseBuilding)
            {
                enabled = false;
                return;
            }

            unitSo = commandable != null ? commandable.UnitSO as UnitSO : null;
            EnsureLineRenderer();
            SetRingVisible(false);
        }

        private void LateUpdate()
        {
            if (!enabled || !showWhenSelected || commandable == null)
            {
                SetRingVisible(false);
                return;
            }

            if (!commandable.IsSelected || !TryGetAttackRange(out float range))
            {
                SetRingVisible(false);
                return;
            }

            SetRingVisible(true);
            UpdateLinePositions(range);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmosInEditor || commandable is BaseBuilding)
            {
                return;
            }

            commandable ??= GetComponent<AbstractCommandable>();

            if (drawGizmosOnlyWhenSelected && commandable != null && !commandable.IsSelected)
            {
                return;
            }

            if (!TryGetAttackRange(out float range))
            {
                return;
            }

            AttackRangeCircleUtility.DrawGizmoCircleXZ(
                transform.position,
                range,
                gizmoColor,
                gizmoSegments);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmosInEditor || commandable is BaseBuilding || !TryGetAttackRange(out float range))
            {
                return;
            }

            AttackRangeCircleUtility.DrawGizmoCircleXZ(
                transform.position,
                range,
                gizmoColor,
                gizmoSegments);
        }

        private void OnDestroy()
        {
            if (runtimeLineMaterial != null)
            {
                Destroy(runtimeLineMaterial);
            }
        }

        public bool TryGetAttackRange(out float range)
        {
            if (commandable is BaseBuilding)
            {
                range = 0f;
                return false;
            }

            commandable ??= GetComponent<AbstractCommandable>();
            unitSo ??= commandable != null ? commandable.UnitSO as UnitSO : null;

            if (attackRangeOverride > 0f)
            {
                range = attackRangeOverride;
                return true;
            }

            if (unitSo?.AttackConfig != null && unitSo.AttackConfig.AttackRange > 0f)
            {
                range = unitSo.AttackConfig.AttackRange;
                return true;
            }

            range = 0f;
            return false;
        }

        private void EnsureLineRenderer()
        {
            if (lineRenderer == null)
            {
                Transform existing = transform.Find("AttackRangeRing");
                if (existing != null)
                {
                    lineRenderer = existing.GetComponent<LineRenderer>();
                }

                if (lineRenderer == null)
                {
                    GameObject ringObject = new("AttackRangeRing");
                    ringObject.transform.SetParent(transform, false);
                    lineRenderer = ringObject.AddComponent<LineRenderer>();
                }
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.positionCount = circleBuffer.Length;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.material = GetOrCreateLineMaterial();
            lineRenderer.startColor = ringColor;
            lineRenderer.endColor = ringColor;
        }

        private Material GetOrCreateLineMaterial()
        {
            if (runtimeLineMaterial != null)
            {
                runtimeLineMaterial.color = ringColor;
                return runtimeLineMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            runtimeLineMaterial = new Material(shader) { color = ringColor };
            return runtimeLineMaterial;
        }

        private void UpdateLinePositions(float range)
        {
            AttackRangeCircleUtility.FillCircleXZ(
                circleBuffer,
                transform.position,
                range,
                groundYOffset);

            lineRenderer.positionCount = circleBuffer.Length;
            lineRenderer.SetPositions(circleBuffer);
        }

        private void SetRingVisible(bool visible)
        {
            if (ringVisible == visible)
            {
                return;
            }

            ringVisible = visible;

            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }
    }
}
