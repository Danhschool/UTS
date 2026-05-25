using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Gán slot P1/P2 theo thứ tự connectionId lần đầu — không đổi khi OnServerAddPlayer gọi lại (đổi scene).
    /// </summary>
    public static class RtsPlayerSlotRegistry
    {
        static readonly Dictionary<int, int> SlotByConnectionId = new();

        public static void Reset()
        {
            SlotByConnectionId.Clear();
        }

        public static void Release(int connectionId)
        {
            SlotByConnectionId.Remove(connectionId);
        }

        /// <summary>
        /// Mục tiêu: Host luôn 0, client thứ hai luôn 1 — kể cả khi add player lại sau ServerChangeScene.
        /// Cách hoạt động: connectionId đã có trong map → trả slot cũ; chưa có → slot = số entry hiện có (0 rồi 1).
        /// </summary>
        public static int AssignOrGet(NetworkConnectionToClient conn)
        {
            if (conn == null)
            {
                return 0;
            }

            int id = conn.connectionId;
            if (SlotByConnectionId.TryGetValue(id, out int existing))
            {
                return existing;
            }

            int slot = Mathf.Clamp(SlotByConnectionId.Count, 0, 1);
            SlotByConnectionId[id] = slot;
            Debug.Log(
                $"[RtsPlayerSlotRegistry] connectionId={id} → slot {slot} ({(slot == 0 ? "P1" : "P2")}), total={SlotByConnectionId.Count}");
            return slot;
        }
    }
}
