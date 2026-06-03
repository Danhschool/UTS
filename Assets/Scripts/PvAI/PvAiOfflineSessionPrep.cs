using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Chuẩn bị session PvE offline và điều phối spawn căn cứ theo lần load scene.
    /// </summary>
    public static class PvAiOfflineSessionPrep
    {
        static int s_loadGeneration;
        static int s_spawnCompletedGeneration = -1;
        static string s_lastSpawnedSceneName;

        public static int CurrentLoadGeneration => s_loadGeneration;

        public static void OnGameplayLoadRequested()
        {
            s_loadGeneration++;
            s_spawnCompletedGeneration = -1;
        }

        /// <summary>
        /// Mục tiêu: PvE offline luôn chơi phe Player1 dù vừa chơi MP client P2.
        /// Cách hoạt động: Clear cache team MP + SetLocalOwner(Player1).
        /// </summary>
        public static void PrepareLocalHumanOwner()
        {
            RtsLocalHumanOwnerNotifier.ClearCachedTeamIndex();
            LocalHumanOwnerService service = LocalHumanOwnerService.EnsureExists();
            service.SetLocalOwner(Owner.Player1);
        }

        public static bool ShouldSpawnForCurrentLoad()
        {
            return s_spawnCompletedGeneration != s_loadGeneration;
        }

        public static void MarkSpawnCompletedForCurrentLoad()
        {
            s_spawnCompletedGeneration = s_loadGeneration;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                s_lastSpawnedSceneName = scene.name;
            }
        }

        /// <summary>
        /// Mục tiêu: Loading bar chờ đúng căn cứ Player1 sau spawn PvE.
        /// Cách hoạt động: Quét BaseBuilding trong scene active.
        /// </summary>
        public static bool HasHumanCivilCentral(Owner humanOwner = Owner.Player1)
        {
            BaseBuilding[] buildings = Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null
                    || building.Owner != humanOwner
                    || !CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_loadGeneration = 0;
            s_spawnCompletedGeneration = -1;
            s_lastSpawnedSceneName = null;
        }
    }
}
