Project RTS — Mirror multiplayer (RTS 2 người) — hướng dẫn nhanh
================================================================

Hướng dẫn CHI TIẾT (chức năng + từng file code + kiến trúc):
  → Assets/3rdParty/RTS_Multiplayer/RTS_Multiplayer_DOCUMENTATION.md

0) Mirror Asset Store: một số bản không còn file UniqueNameAuthenticator.cs trong Mirror.Authenticators.
   Project dùng RtsUniqueNameAuthenticator (Assets/3rdParty/RTS_Multiplayer/Scripts) — không phụ thuộc file đó.
   asmdef: Mirror + Mirror.Components.

1) Cài Mirror: Package Manager → My Assets → Mirror → Import (Asset Store). Đợi compile xanh.

--- Tạo UI & scene (Editor) ---
2) Sinh lobby + map + prefab một lần:
   Menu: ProjectRTS → Netplay → Generate RTS Mirror scenes & prefabs
   (Tạo Canvas, ô tên, Host/Client, chat, Ready, nút Bắt đầu trận; gán RtsUniqueNameAuthenticator + NetworkManager.)

3) Mở scene Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity — kiểm tra Hierarchy:
   - NetworkManager: TelepathyTransport, RtsUniqueNameAuthenticator, RtsNetworkManager (playerPrefab + unitPrefab + spawnPrefabs).

--- Luồng chơi thử ---
4) Một máy: Build & Run = Host; Editor Play cùng scene = Client, IP 127.0.0.1.
   Hai người: chat → cả hai Ready → Host "Bắt đầu trận" → click mặt đất để di chuyển unit.
   File sinh ra sau bước 2: Scenes RtsNet_Lobby / RtsNet_Game, Prefabs RtsNet_Player / RtsNet_Unit.

5) Build Settings: thêm RtsNet_Lobby (index 0). Player: Run In Background = bật.

6) Hai máy LAN:
   - Máy Host: ipconfig → IPv4 (vd 192.168.x.x).
   - Máy Client: cùng Wi‑Fi/LAN, trong ô địa chỉ nhập IP Host (không dùng localhost).
   - Firewall Windows: cho phép cổng 7777 (Telepathy mặc định) khi được hỏi.

7) Lỗi thường gặp:
   - Client không kết nối: sai IP, firewall, hoặc Host chưa bấm Host.
   - Không thấy đơn vị: kiểm tra RtsNetworkManager có gán unitPrefab + unit nằm trong spawnPrefabs (builder đã thêm).
