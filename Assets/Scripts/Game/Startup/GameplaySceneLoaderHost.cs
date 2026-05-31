using System.Collections;
using UnityEngine;

namespace GameDevTV.RTS.Game.Startup
{
    /// <summary>
    /// SRP: Host DontDestroyOnLoad — coroutine loading sống sót khi LoadSceneAsync(Single) hủy Loading scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplaySceneLoaderHost : MonoBehaviour
    {
        static GameplaySceneLoaderHost instance;

        public static GameplaySceneLoaderHost Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject(nameof(GameplaySceneLoaderHost));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameplaySceneLoaderHost>();
            return instance;
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
