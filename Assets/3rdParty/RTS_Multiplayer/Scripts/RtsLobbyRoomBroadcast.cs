using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// SRP: Quảng bá phòng Host qua UDP để client khác thêm nút Join vào scrollview.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsLobbyRoomBroadcast : MonoBehaviour
    {
        const int Port = 47777;
        const string Prefix = "UTS_ROOM|";

        public event Action<string, string> RoomDiscovered;

        UdpClient _advertiser;
        UdpClient _listener;
        Thread _listenThread;
        volatile bool _listening;
        string _roomName;
        string _hostAddress;
        float _nextAdvertiseTime;

        void Update()
        {
            if (_advertiser == null || string.IsNullOrWhiteSpace(_roomName))
            {
                return;
            }

            if (Time.unscaledTime < _nextAdvertiseTime)
            {
                return;
            }

            _nextAdvertiseTime = Time.unscaledTime + 1f;
            SendAdvertisement(_roomName, _hostAddress);
        }

        void OnDestroy()
        {
            StopAdvertising();
            StopListening();
        }

        /// <summary>
        /// Mục tiêu: Host quảng bá phòng sau khi StartHost.
        /// Cách hoạt động: Gửi UDP broadcast + loopback + subnet mỗi giây với tên phòng + IP host.
        /// </summary>
        public void StartAdvertising(string roomName, string hostAddress)
        {
            StopAdvertising();
            _roomName = roomName;
            _hostAddress = hostAddress;
            _advertiser = new UdpClient { EnableBroadcast = true };
            _nextAdvertiseTime = 0f;
        }

        public void StopAdvertising()
        {
            _roomName = null;
            _hostAddress = null;
            _advertiser?.Close();
            _advertiser = null;
        }

        public void StartListening()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;
            _listenThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "RtsLobbyRoomBroadcastListen"
            };
            _listenThread.Start();
        }

        public void StopListening()
        {
            _listening = false;
            try
            {
                _listener?.Close();
            }
            catch
            {
                // ignored
            }

            _listener = null;
        }

        void SendAdvertisement(string roomName, string hostAddress)
        {
            if (_advertiser == null)
            {
                return;
            }

            string payload = Prefix + roomName + "|" + hostAddress;
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            TrySend(bytes, new IPEndPoint(IPAddress.Broadcast, Port));
            TrySend(bytes, new IPEndPoint(IPAddress.Loopback, Port));

            if (TryParseSubnetBroadcast(hostAddress, out IPAddress subnetBroadcast))
            {
                TrySend(bytes, new IPEndPoint(subnetBroadcast, Port));
            }
        }

        void TrySend(byte[] bytes, IPEndPoint target)
        {
            if (_advertiser == null)
            {
                return;
            }

            try
            {
                _advertiser.Send(bytes, bytes.Length, target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RtsLobbyRoomBroadcast] Gửi tới {target} thất bại: {ex.Message}");
            }
        }

        static bool TryParseSubnetBroadcast(string hostAddress, out IPAddress subnetBroadcast)
        {
            subnetBroadcast = null;
            if (!IPAddress.TryParse(hostAddress, out IPAddress hostIp))
            {
                return false;
            }

            byte[] bytes = hostIp.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return false;
            }

            bytes[3] = 255;
            subnetBroadcast = new IPAddress(bytes);
            return true;
        }

        void ListenLoop()
        {
            try
            {
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                IPEndPoint remote = new(IPAddress.Any, Port);

                UnityMainThreadDispatcher.Enqueue(() =>
                    Debug.Log("[RtsLobbyRoomBroadcast] Đang lắng nghe phòng LAN trên port " + Port));

                while (_listening)
                {
                    byte[] data = _listener.Receive(ref remote);
                    string message = Encoding.UTF8.GetString(data);
                    if (!message.StartsWith(Prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string body = message.Substring(Prefix.Length);
                    int split = body.LastIndexOf('|');
                    if (split <= 0 || split >= body.Length - 1)
                    {
                        continue;
                    }

                    string roomName = body.Substring(0, split);
                    string hostAddress = body.Substring(split + 1);
                    UnityMainThreadDispatcher.Enqueue(() => RoomDiscovered?.Invoke(roomName, hostAddress));
                }
            }
            catch (SocketException ex)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                    Debug.LogWarning("[RtsLobbyRoomBroadcast] Listen socket: " + ex.Message));
            }
            catch (Exception ex)
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                    Debug.LogWarning("[RtsLobbyRoomBroadcast] Listen lỗi: " + ex.Message));
            }
        }
    }

    /// <summary>
    /// SRP: Đưa callback UDP thread về main thread Unity.
    /// </summary>
    static class UnityMainThreadDispatcher
    {
        static readonly System.Collections.Generic.Queue<Action> Queue = new();
        static Runner _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootstrapOnMainThread()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Mục tiêu: Tạo runner trên main thread trước khi UDP thread enqueue callback.
        /// Cách hoạt động: Chỉ gọi từ Awake/OnEnable/BeforeSceneLoad — không tạo GameObject từ background thread.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_runner != null)
            {
                return;
            }

            var host = new GameObject(nameof(UnityMainThreadDispatcher));
            host.hideFlags = HideFlags.HideAndDontSave;
            _runner = host.AddComponent<Runner>();
            UnityEngine.Object.DontDestroyOnLoad(host);
        }

        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (Queue)
            {
                Queue.Enqueue(action);
            }
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                while (true)
                {
                    Action action;
                    lock (Queue)
                    {
                        if (Queue.Count == 0)
                        {
                            break;
                        }

                        action = Queue.Dequeue();
                    }

                    action?.Invoke();
                }
            }

            void OnDestroy()
            {
                _runner = null;
            }
        }
    }
}
