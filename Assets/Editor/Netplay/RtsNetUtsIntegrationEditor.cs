#if UNITY_EDITOR
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using Mirror;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.Editor.Netplay
{
    /// <summary>
    /// Mục tiêu: M4 — gắn RtsUtsGameSceneSetup, NetworkIdentity trên prefab UTS, player commands.
    /// </summary>
    public static class RtsNetUtsIntegrationEditor
    {
        const string CivilCentralPrefabPath = "Assets/Prefab/Buildings/civil_central/civil_central.prefab";
        const string WorkerPrefabPath = "Assets/Prefab/Unit/Worker.prefab";
        const string PlayerPrefabPath = "Assets/3rdParty/RTS_Multiplayer/Prefabs/RtsNet_Player.prefab";
        const string GameScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";

        [MenuItem("ProjectRTS/Netplay/M4 Integrate UTS spawn + network components")]
        public static void Integrate()
        {
            EnsureNetworkedPrefabAsset(CivilCentralPrefabPath);
            EnsureNetworkedPrefabAsset(WorkerPrefabPath);
            EnsurePlayerPrefabCommandsAsset();
            WireGameSceneFields();
            AssetDatabase.SaveAssets();
            Debug.Log("[RtsNetUtsIntegration] M4 code wiring xong. Dùng ★ One-Click Play Setup để đăng ký Spawn Prefabs trên Lobby.");
        }

        public static void EnsureNetworkedPrefabAsset(string path)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (!prefabRoot.TryGetComponent(out NetworkIdentity _))
            {
                prefabRoot.AddComponent<NetworkIdentity>();
            }

            if (!prefabRoot.TryGetComponent(out NetworkTransformUnreliable _))
            {
                prefabRoot.AddComponent<NetworkTransformUnreliable>();
            }

            if (!prefabRoot.TryGetComponent(out RtsUtsNetworkEntity _))
            {
                prefabRoot.AddComponent<RtsUtsNetworkEntity>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        public static void EnsurePlayerPrefabCommandsAsset()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (!prefabRoot.TryGetComponent(out RtsUtsPlayerCommands _))
            {
                prefabRoot.AddComponent<RtsUtsPlayerCommands>();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        public static void WireGameSceneFields()
        {
            RtsUtsGameSceneSetup setup = Object.FindFirstObjectByType<RtsUtsGameSceneSetup>();
            if (setup == null)
            {
                GameObject setupGo = GameObject.Find("RtsGameSceneSetup") ?? new GameObject("RtsUtsGameSceneSetup");
                setup = setupGo.GetComponent<RtsUtsGameSceneSetup>();
                if (setup == null)
                {
                    setup = setupGo.AddComponent<RtsUtsGameSceneSetup>();
                }
            }

            setup.civilCentralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilCentralPrefabPath);
            setup.startingWorkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPrefabPath);
            setup.startingWorkerCount = 1;
            setup.disableAiControllersOnLoad = true;

            if (setup.teamSpawnPoints == null || setup.teamSpawnPoints.Length < 2)
            {
                Transform t0 = GameObject.Find("Team0Spawn")?.transform;
                Transform t1 = GameObject.Find("Team1Spawn")?.transform;
                if (t0 != null && t1 != null)
                {
                    setup.teamSpawnPoints = new[] { t0, t1 };
                }
            }

            if (Object.FindFirstObjectByType<LocalHumanOwnerService>() == null)
            {
                new GameObject("LocalHumanOwnerService").AddComponent<LocalHumanOwnerService>();
            }
        }
    }
}
#endif
