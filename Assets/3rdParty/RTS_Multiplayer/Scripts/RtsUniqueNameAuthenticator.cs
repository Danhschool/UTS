using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectRTS.Netplay
{
    /// <summary>
    /// Mục tiêu: Xác thực tên người chơi duy nhất (thay cho UniqueNameAuthenticator bị gỡ khỏi gói Asset Store một số bản).
    /// Cách hoạt động: Client gửi AuthRequestMessage; server kiểm tra HashSet tên, gán authenticationData khi hợp lệ.
    /// </summary>
    public class RtsUniqueNameAuthenticator : NetworkAuthenticator
    {
        readonly HashSet<NetworkConnectionToClient> _connectionsPendingDisconnect = new HashSet<NetworkConnectionToClient>();
        public static readonly HashSet<string> PlayerNames = new HashSet<string>();

        [Header("Client Username")]
        public string playerName;

        [Header("Events")]
        public UnityEvent<string> OnAuthSuccess = new UnityEvent<string>();
        public UnityEvent<string> OnAuthFailure = new UnityEvent<string>();

        public struct AuthRequestMessage : NetworkMessage
        {
            public string authUsername;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public bool success;
            public string message;
        }

        [RuntimeInitializeOnLoadMethod]
        static void ResetStatics()
        {
            PlayerNames.Clear();
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
        }

        void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            if (_connectionsPendingDisconnect.Contains(conn))
                return;

            string name = string.IsNullOrWhiteSpace(msg.authUsername) ? string.Empty : msg.authUsername.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[RtsAuth] Từ chối: tên rỗng. Nhập tên trước khi Host/Client.");
                conn.Send(new AuthResponseMessage { success = false, message = "Username required" });
                conn.isAuthenticated = false;
                StartCoroutine(DelayedDisconnect(conn, 0.25f));
                return;
            }

            if (!PlayerNames.Contains(name))
            {
                PlayerNames.Add(name);
                conn.authenticationData = name;
                conn.Send(new AuthResponseMessage { success = true, message = "Success" });
                ServerAccept(conn);
            }
            else
            {
                Debug.LogWarning($"[RtsAuth] Từ chối: tên đã dùng — \"{name}\"");
                _connectionsPendingDisconnect.Add(conn);
                conn.Send(new AuthResponseMessage
                {
                    success = false,
                    message = "Username already in use...try again"
                });
                conn.isAuthenticated = false;
                StartCoroutine(DelayedDisconnect(conn, 1f));
            }
        }

        IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);
            ServerReject(conn);
            yield return null;
            _connectionsPendingDisconnect.Remove(conn);
        }

        public void SetPlayername(string username)
        {
            playerName = username;
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            string name = string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[RtsAuth] playerName rỗng — nhập tên trong UI trước khi Host/Client.");
                ClientReject();
                return;
            }

            NetworkClient.Send(new AuthRequestMessage { authUsername = name });
        }

        void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.success)
            {
                Debug.Log($"[RtsAuth] OK — \"{playerName}\"");
                OnAuthSuccess.Invoke(msg.message);
                ClientAccept();
            }
            else
            {
                Debug.LogWarning($"[RtsAuth] Thất bại: {msg.message}");
                var nm = NetworkManager.singleton;
                if (nm != null)
                {
                    if (NetworkServer.active && NetworkClient.isConnected)
                        nm.StopHost();
                    else if (NetworkClient.active)
                        nm.StopClient();
                }

                OnAuthFailure.Invoke(msg.message);
            }
        }
    }
}
