using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapInputHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform clickArea;
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MonoBehaviour cameraNavigatorBehaviour;

        private IMinimapCameraNavigator cameraNavigator;

        private void Awake()
        {
            if (clickArea == null)
            {
                clickArea = transform as RectTransform;
            }

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

            if (cameraNavigator == null)
            {
                cameraNavigatorBehaviour = FindFirstObjectByType<PlayerInput>();
                cameraNavigator = cameraNavigatorBehaviour as IMinimapCameraNavigator;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || mapBounds == null || cameraNavigator == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    clickArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect rect = clickArea.rect;
            Vector2 normalized = new(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));

            Vector3 world = mapBounds.NormalizedToWorld(normalized);
            cameraNavigator.PanCameraToWorldPosition(world);
        }
    }
}
