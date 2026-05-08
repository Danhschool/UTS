# Project RTS — Module `RTS_Multiplayer` (Mirror)

Tài liệu mô tả **chức năng**, **luồng mạng**, và **từng file code** trong `Assets/3rdParty/RTS_Multiplayer/`.  
Phiên bản ngắn và bước cài đặt nhanh: xem `RTS_Multiplayer_README.txt`.

---

## 1. Mục tiêu module

- **RTS 2 người** qua LAN / localhost, dùng **Mirror** (server authoritative).
- **Lobby**: đặt tên (xác thực duy nhất), Host/Client, **chat**, **Ready**, Host **bắt đầu trận**.
- **Map chơi**: spawn unit theo phe, **click mặt đất** để di chuyển, **HUD vàng** tăng theo thời gian (MVP).

---

## 2. Cấu trúc thư mục

| Đường dẫn | Nội dung |
|-----------|----------|
| `Scripts/` | Logic runtime (C#), namespace `ProjectRTS.Netplay` |
| `Editor/RtsNetSceneBuilder.cs` | Menu Editor sinh scene + prefab |
| `Scenes/RtsNet_Lobby.unity` | Lobby + NetworkManager + UI |
| `Scenes/RtsNet_Game.unity` | Map chơi + HUD + spawn điểm |
| `Prefabs/RtsNet_Player.prefab` | Player mạng (lobby + commander + economy) |
| `Prefabs/RtsNet_Unit.prefab` | Unit RTS (di chuyển, đồng bộ) |
| `ProjectRTS.Netplay.asmdef` | Assembly: Mirror, Mirror.Components, Unity.InputSystem |
| `Editor/ProjectRTS.Netplay.Editor.asmdef` | Assembly Editor: Netplay + Mirror + InputSystem |

**Phụ thuộc ngoài:** `Assets/3rdParty/Mirror` (cài từ Asset Store), package `com.unity.inputsystem`.

---

## 3. Luồng người dùng (tóm tắt)

1. Mở scene **`RtsNet_Lobby`** → Play.
2. Nhập **tên** → **Host** (một máy) hoặc **Client** + địa chỉ (vd. `127.0.0.1`).
3. Trong lobby: **chat** (tùy chọn) → cả hai **Ready**.
4. **Host** bấm **Bắt đầu trận** → Mirror **`ServerChangeScene`** sang **`RtsNet_Game`**.
5. Trên map: **click chuột trái** lên mặt đất → unit của phe mình di chuyển; xem **Vàng** trên HUD.

---

## 4. Kiến trúc & quyền hạn (Mirror)

| Vai trò | Trách nhiệm |
|---------|-------------|
| **Server** | Xác thực tên, spawn player/unit, áp dụng lệnh di chuyển, tăng vàng, broadcast Rpc chat. |
| **Client** | UI, gửi **Command** (vd. `CmdMoveUnit`), hiển thị SyncVar / transform replicate. |

**Nguyên tắc SOLID (áp dụng trong module):**

- **SRP:** Tách lobby UI (`RtsLobbyUI`), chat (`RtsLobbyChat`), mạng (`RtsNetworkManager`), input (`RtsGameInput`), economy (`RtsPlayerEconomy`), HUD (`RtsGoldHud`).
- **OCP:** Mở rộng prefab/unit mới qua prefab + đăng ký spawn; ít sửa core manager.
- **LSP / ISP:** Component nhỏ, interface hành vi rõ (authenticator, commander).
- **DIP:** UI phụ thuộc Mirror qua singleton / component gán Inspector, không hardcode scene path trong logic UI (chỉ data `Scene` string trên manager).

---

## 5. Các file code (`Scripts/`)

### 5.1 `RtsNetworkManager.cs`

**Chức năng:** Kế thừa `NetworkManager`, giới hạn 2 kết nối, lobby/game scene, spawn unit khi vào map.

**Điểm chính:**

- `lobbyScene` / `gameScene`: tên scene trong Build Settings.
- `unitPrefab`: prefab unit server spawn sau khi load `gameScene`.
- `OnServerAddPlayer`: spawn `playerPrefab`, gọi `RtsLobbyPlayer.ServerInitSlot`.
- `OnServerSceneChanged`: nếu đang ở `gameScene`, đọc `RtsGameSceneSetup.teamSpawnPoints`, spawn mỗi connection một `unitPrefab`, `RtsUnit.ServerAssignOwner(...)`, `NetworkServer.Spawn`.
- `ServerTryStartMatch()`: chỉ host (server + client), yêu cầu ≥2 connection và mọi `RtsLobbyPlayer` `IsReady` → `ServerChangeScene(gameScene)`.
- `OnServerDisconnect`: dọn tên authenticator + chat map connection.
- `OnClientDisconnect`: gọi `RtsLobbyUI.ShowLoginAgain()`.
- `OnClientError` / `OnServerError`: log transport (hỗ trợ debug).

**Inspector:** `playerPrefab`, `unitPrefab`, `spawnPrefabs` (phải chứa `unitPrefab`), `authenticator`, `transport` (Telepathy).

---

### 5.2 `RtsUniqueNameAuthenticator.cs`

**Chức năng:** Thay thế `UniqueNameAuthenticator` không còn trong một số gói Mirror — đảm bảo **tên không trùng** trên server.

**Luồng:** Client gửi `AuthRequestMessage` (tên) → server thêm vào `PlayerNames` hoặc từ chối → `ClientAccept` / thất bại thì `StopHost` hoặc `StopClient` tùy mode.

**Lưu ý:** `playerName` phải được UI gán **trước** `StartHost` / `StartClient`.

---

### 5.3 `RtsLobbyPlayer.cs`

**Chức năng:** Đại diện người chơi: `displayName` (từ auth), `playerTeamIndex`, `ready` (SyncVar).

- `OnStartLocalPlayer`: báo `RtsLobbyUI.OnLocalPlayerAssigned`.
- `CmdSetReady`: server set `ready`.

---

### 5.4 `RtsLobbyUI.cs`

**Chức năng:** Panel đăng nhập / lobby: địa chỉ, tên, Host, Client, Ready, nút bắt đầu (host).

- `WireLobbyButtons()` (Awake): gắn `onClick` bằng code (vì scene có thể không lưu UnityEvent).
- `NormalizeLoopbackAddress` / `RefreshAddressFromUi`: chuẩn hóa `126.0.0.1` → `127.0.0.1`.
- `ServerTryStartMatch` gọi từ nút bắt đầu trận.

---

### 5.5 `RtsLobbyChat.cs`

**Chức năng:** Chat lobby — **chỉ trong scene lobby** (object có `NetworkIdentity`).

- `CmdSend` → server map tên từ connection → `RpcReceive` append lên `Text chatHistory`.
- `UiSendMessage`, `UiOnEndEdit` (Enter): gửi tin; dùng **Input System** (`Keyboard`) cho Enter.

**Lưu ý:** Không tự có trong `RtsNet_Game`; muốn chat trong map cần thêm UI + script tương tự trong scene game.

---

### 5.6 `RtsGameCommander.cs`

**Chức năng:** Nằm trên **player prefab** — kênh **Command** di chuyển unit.

- `CmdMoveUnit(netId, destination)`: server kiểm tra `RtsUnit.ServerCanOrder(connectionId)` rồi `ServerSetDestination`.

---

### 5.7 `RtsGameInput.cs`

**Chức năng:** Local player: **click trái** → raycast mặt đất → tìm `RtsUnit` `IsOwnedLocally` → `RequestMoveUnit`.

- Dùng **Input System** (`Mouse.current`), không dùng `UnityEngine.Input` (tránh lỗi khi Active Input Handling = Input System only).

---

### 5.8 `RtsUnit.cs`

**Chức năng:** Unit đồng bộ — server di chuyển về `serverDestination`; `NetworkTransformUnreliable` replicate.

- `ownerPlayerNetId` (SyncVar): client so với `NetworkClient.localPlayer.netId` để biết unit của mình.
- `ServerAssignOwner`, `ServerSetDestination`, `ServerCanOrder`.

---

### 5.9 `RtsPlayerEconomy.cs`

**Chức năng:** Vàng — **chỉ server** tăng khi scene active là `RtsNet_Game`; `SyncVar gold`.

---

### 5.10 `RtsGoldHud.cs`

**Chức năng:** Client đọc `RtsPlayerEconomy` trên `localPlayer`, cập nhật `Text` HUD (scene game).

---

### 5.11 `RtsGameSceneSetup.cs`

**Chức năng:** Marker trong scene game: mảng `teamSpawnPoints[0..1]` cho hai phe — `RtsNetworkManager` đọc khi spawn unit.

---

## 6. Editor — `RtsNetSceneBuilder.cs`

**Menu:** `ProjectRTS → Netplay → Generate RTS Mirror scenes & prefabs`

**Việc thực hiện:**

- Tạo / ghi đè prefab `RtsNet_Player`, `RtsNet_Unit`.
- Tạo scene `RtsNet_Lobby` (Canvas, NetworkManager + Telepathy + Authenticator, EventSystem với **InputSystemUIInputModule**, chat, nút…).
- Tạo scene `RtsNet_Game` (plane, camera, light, `RtsGameSceneSetup`, HUD, EventSystem **Input System**).
- Gộp scene vào **Build Settings**.

Sau khi chỉnh tay scene/prefab, có thể **không** chạy lại Generate (sẽ ghi đè).

---

## 7. Prefab & NetworkManager (bạn cần biết)

| Mục | Ý nghĩa |
|-----|---------|
| **Player Prefab** | Người chơi Mirror — có `NetworkIdentity`, `RtsLobbyPlayer`, `RtsGameCommander`, `RtsGameInput`, `RtsPlayerEconomy`. |
| **Unit Prefab** | Unit — `NetworkIdentity`, `NetworkTransformUnreliable`, `RtsUnit`. |
| **Spawn Prefabs** | Danh sách Mirror: **bắt buộc** chứa prefab sẽ `Spawn` runtime (vd. unit). |

**Không** cần gắn `NetworkManager` lên từng prefab mới — chỉ **một** `RtsNetworkManager` trong lobby; prefab mới **đăng ký** trong Spawn Prefabs (+ logic spawn tương ứng).

---

## 8. UI & Input System

- **Player Settings → Active Input Handling:** thường **Input System Package** — toàn bộ UI phải dùng **`InputSystemUIInputModule`** trên `EventSystem` (lobby và game đã chỉnh trong scene/module).
- `RtsGameInput` / `RtsLobbyChat` dùng API **Input System**, không dùng `Input` legacy.

---

## 9. Sự cố thường gặp

| Hiện tượng | Hướng xử lý |
|------------|-------------|
| Client không kết nối | Địa chỉ đúng (`127.0.0.1` / IP LAN), Host đã chạy, firewall cổng **7777**. |
| Nút không phản hồi | Đảm bảo `RtsLobbyUI.WireLobbyButtons` chạy; EventSystem dùng Input System UI module. |
| Lỗi Legacy Input | Đừng dùng `StandaloneInputModule` trong scene; kiểm tra `RtsGameInput`. |
| Không thấy unit | `unitPrefab` + trong **Spawn Prefabs**; `RtsGameSceneSetup` đủ 2 spawn point. |
| Chat chỉ ở lobby | Thiết kế — xem mục 5.5; game scene chưa có chat. |

---

## 10. Mở rộng gợi ý (không có sẵn trong MVP)

- Chat trong map: Canvas + `NetworkBehaviour` mới trong `RtsNet_Game`, đăng ký prefab nếu spawn động.
- Nhiều loại unit: nhiều prefab trong Spawn Prefabs + logic spawn trong `RtsNetworkManager` hoặc hệ building/production tách file (SRP).

---

## 11. Cách chạy & kiểm chứng (tối thiểu)

1. **Mirror** import, Console không đỏ.
2. Mở **`Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Lobby.unity`**.
3. **ParrelSync** hoặc Build + Editor: một **Host**, một **Client** `127.0.0.1`, **tên khác nhau**.
4. Lobby: chat (tuỳ chọn) → **Ready** cả hai → Host **Bắt đầu trận**.
5. **Game:** HUD “Vàng” tăng; **click sàn** → unit di chuyển; Console không spam lỗi Input.

**Build:** `RtsNet_Lobby` nên ở scene index **0**; **Player → Run In Background** bật.

---

*Tài liệu này mô tả trạng thái module tại thời điểm tạo file; khi refactor, cập nhật mục tương ứng.*
