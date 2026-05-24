using GameDevTV.RTS.Player;
using Mirror;
using ProjectRTS.Netplay;
using Unity.Cinemachine;
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
            RtsLobbyUI.HideLobbyCanvasForGameplay();
            EnsureGameplayCameraRenders();

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

        /// <summary>
        /// Mục tiêu: Tránh màn hình đen "No cameras rendering" sau load scene MP.
        /// Cách hoạt động: Bật Camera gameplay (không RT) + CinemachineBrain trên Main Camera.
        /// </summary>
        static void EnsureGameplayCameraRenders()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || cam.targetTexture != null)
                {
                    continue;
                }

                if (!cam.gameObject.activeInHierarchy)
                {
                    cam.gameObject.SetActive(true);
                }

                cam.enabled = true;
            }

            Camera main = Camera.main;
            if (main != null)
            {
                CinemachineBrain brain = main.GetComponent<CinemachineBrain>();
                if (brain != null)
                {
                    brain.enabled = true;
                }
            }
        }
    }
}
