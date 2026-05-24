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
        public GameObject startingWorkerPrefab;

        [Header("Owners")]
        public Owner humanOwner = Owner.Player1;
        public Owner aiOwner = Owner.AI2;

        [Header("Starting workers")]
        public bool spawnStartingWorker = true;

        [Min(0)]
        [Tooltip("Số worker spawn cạnh mỗi Civil Central (0 = không spawn).")]
        public int startingWorkerCount = 1;

        public Vector3 workerOffsetFromBase = new(2f, 0f, 2f);

        [Tooltip("Khoảng cách giữa worker thứ 2, 3, … (worker đầu dùng workerOffsetFromBase).")]
        public Vector3 workerSpawnSpacing = new(2f, 0f, 0f);

        [Header("Scene cleanup")]
        public bool destroyScenePlacedCivilCentrals = true;
    }
}
