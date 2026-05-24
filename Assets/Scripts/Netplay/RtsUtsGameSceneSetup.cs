using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Cấu hình prefab UTS và spawn điểm cho scene RtsNet_Game (thay capsule MVP khi đã wire).
    /// </summary>
    public sealed class RtsUtsGameSceneSetup : MonoBehaviour
    {
        [Header("Spawn")]
        [Tooltip("Index 0 = team 0, index 1 = team 1")]
        public Transform[] teamSpawnPoints = new Transform[2];

        [Header("UTS prefabs (cần NetworkIdentity + RtsUtsNetworkEntity)")]
        public GameObject civilCentralPrefab;
        public GameObject startingWorkerPrefab;

        [Header("Scene")]
        [Tooltip("Tắt AIController khi test MP PvP phase 1.")]
        public bool disableAiControllersOnLoad = true;
    }
}
