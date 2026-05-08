using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Click mặt đất để ra lệnh di chuyển cho đơn vị thuộc local player (MVP 1 unit/phe).
    /// Cách hoạt động: Raycast từ camera; tìm RtsUnit IsOwnedLocally rồi gọi RequestMoveUnit trên RtsGameCommander.
    /// </summary>
    [RequireComponent(typeof(RtsGameCommander))]
    public class RtsGameInput : MonoBehaviour
    {
        [SerializeField] LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] Camera gameplayCamera;

        RtsGameCommander _commander;

        void Awake()
        {
            _commander = GetComponent<RtsGameCommander>();
        }

        void Update()
        {
            if (!_commander.isLocalPlayer)
                return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (gameplayCamera == null)
                return;

            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = gameplayCamera.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
                return;

            var units = FindObjectsByType<RtsUnit>(FindObjectsSortMode.None);
            foreach (var u in units)
            {
                if (!u.IsOwnedLocally)
                    continue;
                _commander.RequestMoveUnit(u.netId, hit.point);
                break;
            }
        }
    }
}
