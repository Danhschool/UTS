using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    public interface IMinimapCameraNavigator
    {
        void PanCameraToWorldPosition(Vector3 worldPosition);
        Transform CameraTargetTransform { get; }
        Camera GameplayCamera { get; }
    }
}
