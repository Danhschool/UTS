using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Tài nguyên đơn giản (vàng) replicate từ server — mở rộng cho đồ án (nhà, thu thập).
    /// Cách hoạt động: Chỉ server tăng gold khi scene đang là map chơi; SyncVar đồng bộ xuống UI client.
    /// </summary>
    public class RtsPlayerEconomy : NetworkBehaviour
    {
        [SerializeField] string gameSceneName = "Game 1";
        [SerializeField] float goldPerSecond = 3f;

        [SyncVar]
        int gold;

        public int Gold => gold;

        [ServerCallback]
        void FixedUpdate()
        {
            if (!isServer)
                return;
            if (SceneManager.GetActiveScene().name != gameSceneName)
                return;
            gold += Mathf.RoundToInt(goldPerSecond * Time.fixedDeltaTime);
        }
    }
}
