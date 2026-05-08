using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Hiển thị vàng của local player trên client (đọc SyncVar từ RtsPlayerEconomy).
    /// Cách hoạt động: Mỗi frame lấy NetworkClient.localPlayer và cập nhật Text.
    /// </summary>
    public class RtsGoldHud : MonoBehaviour
    {
        [SerializeField] Text goldLabel;

#if UNITY_EDITOR
        public void EditorAssignGoldLabel(Text label)
        {
            goldLabel = label;
        }
#endif

        void Update()
        {
            if (goldLabel == null)
                return;
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
            {
                goldLabel.text = "—";
                return;
            }

            var eco = NetworkClient.localPlayer.GetComponent<RtsPlayerEconomy>();
            goldLabel.text = eco != null ? $"Vàng: {eco.Gold}" : "—";
        }
    }
}
