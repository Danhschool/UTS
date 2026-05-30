using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Phím tắt debug đổi local human owner khi test fog P1/P2 (Editor / Development Build).
    /// </summary>
    public sealed class FogLocalOwnerDebugHotkeys : MonoBehaviour
    {
        [SerializeField] Key player1Key = Key.F1;
        [SerializeField] Key player2Key = Key.F2;

        void Awake()
        {
            // Prefab/scene có thể còn số KeyCode cũ (282–293) sau khi đổi field Key → Key.
            player1Key = InputSystemKeyboardUtility.CoerceKeyboardKey(player1Key, Key.F1);
            player2Key = InputSystemKeyboardUtility.CoerceKeyboardKey(player2Key, Key.F2);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            player1Key = InputSystemKeyboardUtility.CoerceKeyboardKey(player1Key, Key.F1);
            player2Key = InputSystemKeyboardUtility.CoerceKeyboardKey(player2Key, Key.F2);
        }
#endif

        void Reset()
        {
            player1Key = Key.F1;
            player2Key = Key.F2;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null)
            {
                return;
            }

            if (InputSystemKeyboardUtility.WasPressedThisFrame(player1Key))
            {
                service.SetLocalOwner(Owner.Player1);
                LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            }
            else if (InputSystemKeyboardUtility.WasPressedThisFrame(player2Key))
            {
                service.SetLocalOwner(Owner.Player2);
                LocalHumanPresentationRefresh.RefreshFromLocalOwner();
            }
#endif
        }
    }
}
