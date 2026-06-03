using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Tự động chọn địa chỉ mạng cho Host/Client lobby.
    /// </summary>
    public static class RtsNetworkAddressUtility
    {
        /// <summary>
        /// Mục tiêu: Lấy IPv4 LAN phù hợp để client kết nối tới host.
        /// Cách hoạt động: Ưu tiên card mạng đang Up, không loopback, không APIPA.
        /// </summary>
        public static string GetBestLanIpv4()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    string ip = address.Address.ToString();
                    if (string.IsNullOrWhiteSpace(ip) || ip.StartsWith("169.254."))
                    {
                        continue;
                    }

                    return ip;
                }
            }

            return "127.0.0.1";
        }

        public static string GetLoopbackOrLan()
        {
            string lan = GetBestLanIpv4();
            return string.IsNullOrWhiteSpace(lan) ? "127.0.0.1" : lan;
        }

        /// <summary>
        /// Mục tiêu: Chọn địa chỉ Mirror Telepathy khi client join phòng qua UDP.
        /// Cách hoạt động: Nếu host quảng bá IP của chính máy này (ParrelSync cùng PC), dùng 127.0.0.1.
        /// </summary>
        public static string ResolveClientConnectAddress(string broadcastHostAddress)
        {
            if (string.IsNullOrWhiteSpace(broadcastHostAddress))
            {
                return "127.0.0.1";
            }

            if (broadcastHostAddress == "127.0.0.1" || broadcastHostAddress == "localhost")
            {
                return "127.0.0.1";
            }

            string localLan = GetBestLanIpv4();
            if (broadcastHostAddress == localLan)
            {
                return "127.0.0.1";
            }

            return broadcastHostAddress;
        }
    }
}
