using GameDevTV.RTS.Player;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Sau khi load RtsNet_Game — đảm bảo LocalOwner/fog/UI MP hoạt động; tắt input capsule MVP.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class RtsNetGameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] string gameSceneName = "RtsNet_Game";
        [SerializeField] bool disableCapsuleGameInput = true;

        void Start()
        {
            if (SceneManager.GetActiveScene().name != gameSceneName)
            {
                return;
            }

            LocalHumanOwnerService.EnsureExists();

            if (disableCapsuleGameInput && NetworkClient.localPlayer != null)
            {
                RtsGameInput capsuleInput = NetworkClient.localPlayer.GetComponent<RtsGameInput>();
                if (capsuleInput != null)
                {
                    capsuleInput.enabled = false;
                }
            }

            PlayerViewBinder binder = FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            if (binder != null)
            {
                binder.RefreshFromLocalOwner();
            }
        }
    }
}
