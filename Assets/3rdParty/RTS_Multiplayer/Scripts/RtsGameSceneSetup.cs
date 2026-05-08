using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Đánh dấu spawn điểm đơn vị theo phe trên scene chơi.
    /// Cách hoạt động: NetworkManager server đọc mảng transform khi load scene game để spawn prefab unit.
    /// </summary>
    public class RtsGameSceneSetup : MonoBehaviour
    {
        [Tooltip("Index 0 = team 0, index 1 = team 1")]
        public Transform[] teamSpawnPoints = new Transform[2];
    }
}
