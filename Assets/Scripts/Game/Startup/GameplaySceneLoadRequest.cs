using GameDevTV.RTS.Game.Startup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Gắn lên UI MainMenu / lobby offline — gọi GameplaySceneLoader.RequestLoad.
    /// </summary>
    public sealed class GameplaySceneLoadRequest : MonoBehaviour
    {
        [SerializeField] string targetScene = "Game 1";
        [SerializeField] LoadSceneMode loadMode = LoadSceneMode.Single;

        /// <summary>
        /// Mục tiêu: Wire vào UnityEvent nút Play offline.
        /// </summary>
        public void LoadTargetScene()
        {
            GameplaySceneLoader.RequestLoad(targetScene, loadMode);
        }

        public void LoadSceneByName(string sceneName)
        {
            GameplaySceneLoader.RequestLoad(sceneName, loadMode);
        }
    }
}
