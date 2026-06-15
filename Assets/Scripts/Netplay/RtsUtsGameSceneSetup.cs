using GameDevTV.RTS.TechTree;
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

        [Header("Starting workers")]
        [Min(0)]
        [Tooltip("Số worker spawn cạnh mỗi Civil Central khi vào trận MP.")]
        public int startingWorkerCount = 1;

        public Vector3 workerOffsetFromBase = new(2f, 0f, 2f);
        public Vector3 workerSpawnSpacing = new(2f, 0f, 0f);

        [Header("MP catalog")]
        [Tooltip("UnlockableSO dùng trong trận MP — server resolve tên asset khi train/build/research.")]
        public UnlockableSO[] unlockableCatalog;

        [Tooltip("Prefab bổ sung cần NetworkServer.Spawn (nhà, unit…) — thêm NetworkIdentity trên prefab.")]
        public GameObject[] additionalNetworkSpawnPrefabs;

        [Header("Scene")]
        [Tooltip("Tắt AIController khi test MP PvP phase 1.")]
        public bool disableAiControllersOnLoad = true;
    }
}
