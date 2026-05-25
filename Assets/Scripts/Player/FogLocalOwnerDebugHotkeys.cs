using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Phím tắt debug đổi local human owner khi test fog P1/P2 trên scene (Editor / Development Build).
    /// </summary>
    public sealed class FogLocalOwnerDebugHotkeys : MonoBehaviour
    {
        [SerializeField] KeyCode player1Key = KeyCode.F1;
        [SerializeField] KeyCode player2Key = KeyCode.F2;

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null)
            {
                return;
            }

            if (Input.GetKeyDown(player1Key))
            {
                service.SetLocalOwner(Owner.Player1);
                LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            }
            else if (Input.GetKeyDown(player2Key))
            {
                service.SetLocalOwner(Owner.Player2);
                LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            }
#endif
        }
    }
}
