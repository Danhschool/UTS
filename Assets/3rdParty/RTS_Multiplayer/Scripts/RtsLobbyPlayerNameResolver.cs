using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Đọc tên người chơi đã lưu từ Settings và thêm hậu tố Host/Client.
    /// </summary>
    public static class RtsLobbyPlayerNameResolver
    {
        const string PlayerNamePrefKey = "RTS.Settings.PlayerName";

        /// <summary>
        /// Mục tiêu: Lấy tên gốc từ PlayerPrefs (Settings MainMenu).
        /// Cách hoạt động: Đọc key đã lưu; fallback tên máy nếu chưa có.
        /// </summary>
        public static string ReadBaseName()
        {
            if (!PlayerPrefs.HasKey(PlayerNamePrefKey))
            {
                return GetMachineDefaultName();
            }

            string raw = PlayerPrefs.GetString(PlayerNamePrefKey);
            return string.IsNullOrWhiteSpace(raw) ? GetMachineDefaultName() : raw.Trim();
        }

        public static string ForHost() => ReadBaseName() + "H";

        public static string ForClient() => ReadBaseName() + "C";

        static string GetMachineDefaultName()
        {
            string name = System.Environment.MachineName;
            return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        }
    }
}
