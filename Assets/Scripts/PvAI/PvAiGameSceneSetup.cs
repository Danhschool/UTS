using GameDevTV.RTS.Utilities;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.PvAI
{
    /// <summary>
    /// SRP: Cấu hình spawn offline PvE trên scene Game 1 (prefab + điểm xuất phát, không dùng CC cắm sẵn).
    /// </summary>
    public sealed class PvAiGameSceneSetup : MonoBehaviour
    {
        [Header("Spawn points (0 = human, 1 = AI)")]
        [Tooltip("Index 0 = người (Player1), index 1 = bot (AI2)")]
        public Transform[] factionSpawnPoints = new Transform[2];

        [Header("Prefabs (cùng asset MP; runtime sẽ gỡ component Mirror)")]
        public GameObject civilCentralPrefab;

        [Header("Owners")]
        public Owner humanOwner = Owner.Player1;
        public Owner aiOwner = Owner.AI2;

        [Header("Starting units (spawn tại rìa CC)")]
        [Tooltip("Bật spawn các unit khởi đầu sau khi Civil Central được tạo.")]
        public bool spawnStartingUnits = true;

        [Tooltip("Mỗi phần tử: prefab unit + số lượng. Unit đặt tại unitSpawnPoint của CC; các slot sau dàn theo unitSpawnSpacing.")]
        public StartingUnitSpawnEntry[] startingUnits = System.Array.Empty<StartingUnitSpawnEntry>();

        [Tooltip("Offset local thêm cho slot đầu (0 = đúng rìa CC / unitSpawnPoint).")]
        public Vector3 unitSpawnFirstOffsetLocal = Vector3.zero;

        [Min(0.1f)]
        [Tooltip("Bán kính sphere giữ chỗ khi spawn. Khoảng cách tâm giữa hai unit = 2 × giá trị này (mặc định 3 → cách nhau 6).")]
        public float unitSpawnSphereRadius = 3f;

        [Header("Scene cleanup")]
        public bool destroyScenePlacedCivilCentrals = true;

        [Header("Legacy (chỉ dùng khi startingUnits trống)")]
        [HideInInspector] public GameObject startingWorkerPrefab;
        [HideInInspector] public bool spawnStartingWorker = true;
        [HideInInspector] public int startingWorkerCount = 1;
        [HideInInspector] public Vector3 workerOffsetFromBase = new(2f, 0f, 2f);
        [HideInInspector] public Vector3 workerSpawnSpacing = new(2f, 0f, 0f);
    }
}
