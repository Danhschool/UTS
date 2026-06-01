# CHƯƠNG 4 — TRIỂN KHAI HỆ THỐNG

Phần **4.1 — Trình bày gameplay cốt lõi** đã hoàn thành trong báo cáo Word. Chương này (từ mục 4.2 trở đi) trình bày cách cài đặt và minh họa bằng mã nguồn ba hệ thống mở rộng: **chơi mạng LAN**, **trí tuệ nhân tạo (AI)** và **nhận diện giọng nói tiếng Việt**. Sơ đồ kiến trúc và luồng xử lý đã có trong báo cáo Word; phần dưới tập trung vào **mô tả triển khai** và **đoạn mã minh họa** tương ứng từng tiểu mục.

> **Ghi chú minh họa:** Mỗi tiểu mục kèm **Hình 4.x. Mã minh họa chức năng …** — chụp đoạn mã nguồn tương ứng trong môi trường lập trình (file `.cs` đã nêu trong bảng phụ lục).

---

## 4.2 Vận dụng Mirror xây dựng chế độ chơi mạng

Chế độ chơi nhiều người được xây dựng trên thư viện **Mirror** — lớp mạng cấp cao cho Unity, cung cấp các thành phần `NetworkManager`, `NetworkBehaviour`, `SyncVar` và cơ chế `[Command]`/`[ClientRpc]`. Dự án áp dụng mô hình **máy chủ có thẩm quyền**: một máy đóng vai trò máy chủ (thường trùng với máy chủ phòng — Host) nắm giữ trạng thái đúng của trận đấu; mọi thay đổi gameplay quan trọng phải được máy chủ xác nhận trước khi đồng bộ xuống các máy khách.

Tầng vận chuyển dùng **Telepathy** (giao thức TCP, cổng 7777): máy Host lắng nghe kết nối đến, máy Client kết nối tới địa chỉ IP của Host — đây là cơ chế cho phép hai máy trong cùng mạng LAN trao đổi gói tin Mirror. Giới hạn `maxConnections = 2` được gán trong `RtsNetworkManager` để phù hợp game RTS hai người.

Mã nguồn Mirror tích hợp nằm tại `Assets/3rdParty/RTS_Multiplayer/`; các lớp cầu nối nối gameplay UTS với Mirror nằm tại `Assets/Scripts/Netplay/` — tách biệt logic game khỏi API mạng.

### 4.2.1 Quản lý phiên mạng và đồng bộ scene

`RtsNetworkManager` kế thừa `NetworkManager` của Mirror, mở rộng vòng đời phiên mạng: quản lý số kết nối, đăng ký đối tượng người chơi khi máy khách tham gia, và **đồng bộ chuyển scene** trên toàn bộ máy khách thông qua `ServerChangeScene`.

Khi Host quyết định bắt đầu trận, máy chủ gọi `ServerChangeScene(gameScene)`. Mirror tự động thông báo mọi máy khách chuyển sang cùng scene; dự án chèn scene trung gian `Loading` để các máy tải tài nguyên đồng bộ trước khi vào bản đồ chơi:

```csharp
// Assets/3rdParty/RTS_Multiplayer/Scripts/RtsNetworkManager.cs
public override void Awake()
{
    base.Awake();
    maxConnections = 2;
}

public override void ServerChangeScene(string newSceneName)
{
    if (newSceneName == gameScene
        && !string.IsNullOrWhiteSpace(loadingScene)
        && !IsActiveScene(loadingScene))
    {
        RtsNetworkSceneLoadHooks.BeginNetworkGameplayLoad?.Invoke(gameScene);
        base.ServerChangeScene(loadingScene);
        return;
    }
    base.ServerChangeScene(newSceneName);
}

public override void OnServerAddPlayer(NetworkConnectionToClient conn)
{
    GameObject player = Instantiate(playerPrefab, pos, rot);
    int slot = RtsPlayerSlotRegistry.AssignOrGet(conn);
    player.GetComponent<RtsLobbyPlayer>()?.ServerInitSlot(slot);
    NetworkServer.AddPlayerForConnection(conn, player);
}
```

Mỗi kết nối được gán một **đối tượng người chơi** (prefab `RtsNet_Player`) chứa `NetworkIdentity` — điểm neo Mirror dùng để gắn lệnh `[Command]` từ máy khách lên máy chủ. Hàm `OnServerSceneChanged` kích hoạt khởi tạo gameplay khi máy chủ đã tải xong scene trận.

**Hình 4.5. Mã minh họa chức năng quản lý phiên mạng và đồng bộ scene (`RtsNetworkManager`)**

![Hình 4.5. Mã minh họa chức năng quản lý phiên mạng và đồng bộ scene (`RtsNetworkManager`)](images/chuong4/hinh-4-5.png)

---

### 4.2.2 Xác thực kết nối và định danh người chơi

Trước khi Mirror cho phép máy khách tham gia phiên, `RtsUniqueNameAuthenticator` (kế thừa `NetworkAuthenticator`) thực hiện bước **kiểm soát truy cập**: máy khách gửi `AuthRequestMessage` qua kênh `NetworkMessage`; máy chủ kiểm tra tên không trùng trong tập hợp `PlayerNames`, rồi gọi `ServerAccept` hoặc ngắt kết nối.

Cơ chế này tách biệt **xác thực tầng vận chuyển** (TCP đã kết nối) với **xác thực tầng ứng dụng** (tên hợp lệ, duy nhất). Tên được lưu vào `conn.authenticationData` để các thành phần khác (chat, hiển thị phòng chờ) tra cứu sau này:

```csharp
// Assets/3rdParty/RTS_Multiplayer/Scripts/RtsUniqueNameAuthenticator.cs
public struct AuthRequestMessage : NetworkMessage
{
    public string authUsername;
}

void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
{
    string name = msg.authUsername?.Trim();
    if (string.IsNullOrEmpty(name) || PlayerNames.Contains(name))
        return; // từ chối

    PlayerNames.Add(name);
    conn.authenticationData = name;
    ServerAccept(conn);
}

// Assets/3rdParty/RTS_Multiplayer/Scripts/RtsNetworkManager.cs
public override void OnServerDisconnect(NetworkConnectionToClient conn)
{
    if (conn.authenticationData is string name)
        RtsUniqueNameAuthenticator.PlayerNames.Remove(name);
    // ...
}
```

Như vậy Mirror không chỉ kết nối hai máy mà còn gắn **định danh logic** cho từng `NetworkConnectionToClient` trước khi khởi tạo đối tượng người chơi.

**Hình 4.6. Mã minh họa chức năng xác thực kết nối và định danh người chơi (`RtsUniqueNameAuthenticator`)**

![Hình 4.6. Mã minh họa chức năng xác thực kết nối và định danh người chơi (`RtsUniqueNameAuthenticator`)](images/chuong4/hinh-4-6.png)

---

### 4.2.3 Đồng bộ lệnh gameplay qua mạng

Gameplay UTS ban đầu thiết kế cho chế độ một máy: `PlayerInput` gọi trực tiếp `BaseCommand.Handle()`. Trong chơi mạng, thay đổi trạng thái đơn vị phải do máy chủ thực thi. Dự án giải quyết bằng ba lớp:

1. **`PlayerInputNetworkBridge`** — giao diện trừu tượng cho phép `PlayerInput` hỏi “có đang ở chế độ máy khách thuần không?” mà không phụ thuộc trực tiếp Mirror.
2. **`RtsUtsClientCommandRelay`** — nếu là máy khách thuần, chuyển input thành yêu cầu gửi lên máy chủ thay vì xử lý cục bộ.
3. **`RtsUtsPlayerCommands`** — thành phần mạng trên prefab người chơi, định nghĩa lệnh `[Command]` để máy chủ nhận và thực thi.

```csharp
// Assets/Scripts/Netplay/RtsUtsClientCommandRelay.cs
PlayerInputNetworkBridge.IsMultiplayerClient = () =>
    NetworkClient.active && NetworkClient.isConnected && !NetworkServer.active;

// Máy khách thuần: chuyển tiếp lệnh di chuyển thay vì xử lý cục bộ
commands.RequestUtsMove(identity.netId, hit.point, unitIndex);

// Assets/Scripts/Netplay/RtsUtsPlayerCommands.cs
[Command]
void CmdUtsMoveUnit(uint netId, Vector3 destination, int formationIndex)
{
    if (!TryResolveCommandable(netId, out var networkEntity, out var unit))
        return;
    moveCommand.Handle(new CommandContext(networkEntity.UtsOwner, unit, hit, formationIndex));
}
```

**`RtsUtsNetworkEntity`** gắn trên prefab đơn vị/công trình, đồng bộ phe (`Owner`) qua `[SyncVar]`; máy chủ kiểm tra quyền qua `ServerCanAcceptOrdersFrom(connectionId)`. Máy khách chỉ chuyển tiếp lệnh lên thực thể thuộc phe mình. Máy Host (vừa là máy chủ vừa là máy khách) thực thi trực tiếp trên máy chủ — không cần chuyển tiếp.

Đây là cách Mirror vận dụng lệnh `[Command]` để biến thao tác cục bộ thành hành động có thẩm quyền trên máy chủ, rồi đồng bộ trạng thái đơn vị cho mọi máy.

**Hình 4.7. Mã minh họa chức năng đồng bộ lệnh gameplay qua Mirror (`RtsUtsClientCommandRelay`, `RtsUtsPlayerCommands`)**

![Hình 4.7. Mã minh họa chức năng đồng bộ lệnh gameplay qua Mirror (`RtsUtsClientCommandRelay`, `RtsUtsPlayerCommands`)](images/chuong4/hinh-4-7.png)

---

### 4.2.4 Khởi tạo thực thể mạng và ánh xạ phe

Sau khi Mirror đồng bộ scene trận, **chỉ máy chủ** được phép khởi tạo đối tượng có `NetworkIdentity` (`NetworkServer.Spawn`). `RtsMatchServerSpawnOrchestrator` lắng nghe sự kiện máy chủ vào scene game, duyệt từng kết nối đã có `RtsLobbyPlayer`, rồi ủy quyền cho `RtsUtsServerSpawnHandler` tạo Căn cứ chính và Nông dân trên máy chủ:

```csharp
// Assets/Scripts/Netplay/RtsUtsServerSpawnHandler.cs
static void HandleSpawnRequest(RtsServerSpawnRequest request)
{
    Owner owner = OwnerTeamMapping.FromTeamIndex(request.TeamIndex);
    SpawnEntity(setup.civilCentralPrefab, spawn, connection, owner);
    SpawnStartingWorkers(setup, spawn, connection, owner);
}

// Assets/Scripts/Netplay/RtsUtsNetworkEntity.cs
[SyncVar(hook = nameof(HookUtsOwner))]
Owner utsOwner;

[Server]
public void ServerConfigure(int connectionId, Owner owner)
{
    serverOwnerConnectionId = connectionId;
    utsOwner = owner; // đồng bộ phe và sương mù chiến tranh trên mọi máy khách
}
```

`OwnerTeamMapping.FromTeamIndex` ánh xạ vị trí trong phòng chờ (0/1) sang `Owner.Player1` / `Owner.Player2` của gameplay UTS. Prefab khởi tạo phải có `NetworkIdentity` và `RtsUtsNetworkEntity` để Mirror đồng bộ và gameplay nhận diện phe, sương mù chiến tranh, quyền ra lệnh.

Trong chế độ hai người qua LAN, `disableAiControllersOnLoad` vô hiệu hóa `AIController` — tránh bot can thiệp khi hai người chơi thật đã kết nối.

Kết quả: hai máy không chỉ **kết nối TCP** mà chia sẻ cùng **không gian trạng thái** — thực thể, phe, lệnh đơn vị — do máy chủ làm nguồn sự thật và Mirror đồng bộ xuống máy khách.

**Hình 4.8. Mã minh họa chức năng khởi tạo thực thể mạng và ánh xạ phe (`RtsUtsServerSpawnHandler`, `RtsUtsNetworkEntity`)**

![Hình 4.8. Mã minh họa chức năng khởi tạo thực thể mạng và ánh xạ phe (`RtsUtsServerSpawnHandler`, `RtsUtsNetworkEntity`)](images/chuong4/hinh-4-8.png)

---

## 4.3 Cài đặt AI — tầng chiến lược (macro)

Tầng AI chiến lược là bộ **ra quyết định vĩ mô** cho phe bot (`Owner.AI2`): mỗi chu kỳ, hệ thống quan sát trạng thái trận (tài nguyên, đơn vị, công trình, kẻ địch trong tầm nhìn), sinh **ý định lệnh** rồi giao cho đơn vị/công trình thực thi qua cùng cơ chế `BaseCommand` như người chơi. Tầng này **không** điều khiển từng bước di chuyển hay hoạt ảnh — phần đó do runtime đơn vị xử lý sau khi nhận lệnh.

Toàn bộ logic nằm tại `Assets/Scripts/AI/`; điểm vào là `AIController`. Trong chế độ chơi mạng LAN, `AIController` bị tắt để chỉ hai người chơi thật đối đầu.

### 4.3.1 Chu kỳ quyết định và bản chụp trạng thái

`AIController` chạy vòng lặp định kỳ (khoảng 1 giây, điều chỉnh theo `AIDifficultySO`). Mỗi chu kỳ gồm ba bước:

1. **`AIUnitRegistry`** — thu thập mọi đơn vị/công trình thuộc phe AI từ sự kiện sinh ra/hủy trong trận.
2. **`AIWorldState.BuildSnapshot()`** — gom dữ liệu chỉ đọc: đá/gỗ/lương thực, dân số, danh sách nông dân/quân/công trình, Căn cứ chính, các mỏ còn trong tầm nhìn sương mù.
3. **`AIPlannerTickPlan`** — quyết định chu kỳ này chạy bộ hoạch định nào (kinh tế mỗi chu kỳ, cơ sở/quân sự cách vài chu kỳ) để giảm tải bộ xử lý.

```csharp
// Assets/Scripts/AI/Core/AIController.cs
public void Tick()
{
    plannerTickIndex++;
    AIPlannerTickPlan plan = AIPlannerTickPlan.Build(plannerTickIndex, ...);
    AIWorldStateSnapshot snapshot = worldState.BuildSnapshot(registry, aiOwner);

    if (plan.RefreshSight)
        AIFactionSightQuery.RefreshFromSnapshot(snapshot);

    RunPlannerDispatch(snapshot, plan);
}
```

`AIWorldStateSnapshot` là **bản chụp trạng thái tại một thời điểm** — mọi bộ hoạch định đọc cùng một bản chụp trong chu kỳ, tránh xung đột khi nhiều module cùng ra quyết định.

**Hình 4.9. Mã minh họa chức năng chu kỳ quyết định và bản chụp trạng thái AI (`AIController.Tick`, `AIWorldStateSnapshot`)**

![Hình 4.9. Mã minh họa chức năng chu kỳ quyết định và bản chụp trạng thái AI (`AIController.Tick`, `AIWorldStateSnapshot`)](images/chuong4/hinh-4-9.png)

---

### 4.3.2 Ba bộ hoạch định chiến lược

Sau khi có bản chụp trạng thái, `RunPlannerDispatch` gọi lần lượt ba module — mỗi module **chỉ đưa ý định lệnh vào hàng đợi**, không gọi gameplay trực tiếp:

| Bộ hoạch định | Lớp mã nguồn | Nhiệm vụ chính |
|---------------|--------------|----------------|
| **Kinh tế** | `AIEconomyManager` | Phân công nông dân thu thập (đá/gỗ/lương thực), Chuồng nuôi, Kho xa căn cứ, săn thú khi thiếu lương thực |
| **Cơ sở** | `AIBaseManager` | Huấn luyện nông dân, xây Kho → Chuồng → Lò rèn → Doanh trại → Tháp, nghiên cứu nâng cấp |
| **Quân sự** | `AIMilitaryManager` | Phòng thủ căn cứ, huấn luyện lính, xây tháp, trinh sát, tập kết và tấn công theo sương mù |

Mỗi module đọc cấu hình thời gian chạy, được suy ra từ tài nguyên cấu hình (`ScriptableObject`) và lớp phủ độ khó. **`AIInfluenceMap`** cung cấp ngữ cảnh vị trí (mối đe dọa, cụm kinh tế) để chọn chỗ xây hoặc hướng tấn công:

```csharp
// Assets/Scripts/AI/Managers/AIBaseManager.cs
public void EnqueueIntents(
    AIWorldStateSnapshot snapshot,
    AIPriorityQueue queue,
    in AIInfluenceMapTickContext influence = default,
    AIDifficultyRuntimeOverlay difficulty = default)
{
    AIBaseRuntimeConfig config = AIBaseConfigResolver.Resolve(snapshot, manualOverrides, difficulty);
    EnqueueTrainWorkerIfNeeded(snapshot, queue, config);
    EnqueueNextScheduledBuildingIfNeeded(snapshot, queue, config, ccPosition, influence);
    EnqueueForgeResearchIfNeeded(snapshot, queue, config);
}

// Assets/Scripts/AI/Managers/AIMilitaryManager.cs
public void EnqueueIntents(AIWorldStateSnapshot snapshot, AIPriorityQueue queue, ...)
{
    AIMilitaryRuntimeConfig config = AIMilitaryConfigResolver.Resolve(...);
    // Quét mối đe dọa → phòng thủ → huấn luyện → tấn công
}
```

**Hình 4.10. Mã minh họa chức năng bộ hoạch định sinh ý định lệnh (`AIBaseManager`, `AIMilitaryManager`)**

![Hình 4.10. Mã minh họa chức năng bộ hoạch định sinh ý định lệnh (`AIBaseManager`, `AIMilitaryManager`)](images/chuong4/hinh-4-10.png)

---

### 4.3.3 Hàng đợi ưu tiên và thực thi lệnh

Các ý định lệnh được đóng gói trong `AICommandIntent` (mức ưu tiên, thực thể, lệnh, điểm va chạm) và đưa vào **`AIPriorityQueue`**. Hàng đợi sắp xếp theo mức ưu tiên giảm dần — ý định quan trọng hơn (ví dụ phòng thủ căn cứ) được lấy ra trước ý định kinh tế.

`AICommandDispatcher` lấy từng ý định và gọi `BaseCommand.Handle()` qua `CommandContext` — **cùng luồng với người chơi**, không qua giao diện. `AIWorkerCommandGuard` ngăn gán lệnh mới lên nông dân đang bận xây dựng hoặc thu thập:

```csharp
// Assets/Scripts/AI/Core/AICommandIntent.cs
public readonly struct AICommandIntent : IComparable<AICommandIntent>
{
    public int Priority { get; }
    public AbstractCommandable Entity { get; }
    public BaseCommand Command { get; }
    public RaycastHit Hit { get; }
}

// Assets/Scripts/AI/Core/AIController.cs — RunPlannerDispatch (rút gọn)
while (priorityQueue.TryPop(out AICommandIntent intent))
{
    if (!AIWorkerCommandGuard.ShouldEnqueue(intent))
        continue;

    if (intent.Entity is AbstractUnit unit)
        commandDispatcher.TryDispatchSpecificCommand(unit, command, intent.Hit, ...);
    else if (intent.Entity is BaseBuilding building)
        commandDispatcher.DispatchBuildingCommand(building, command, intent.Hit);
}
```

Như vậy AI chiến lược hoạt động theo mô hình **quan sát → lập kế hoạch → xếp hàng → thực thi**: quan sát bản chụp trạng thái → bộ hoạch định sinh ý định → hàng đợi ưu tiên → bộ phát lệnh ra lệnh gameplay. Đơn vị nhận lệnh (thu thập, xây dựng, tấn công, …) và tự xử lý chi tiết ở tầng dưới.

**Hình 4.11. Mã minh họa chức năng thực thi ý định lệnh qua bộ phát lệnh (`AICommandIntent`, `AICommandDispatcher`)**

![Hình 4.11. Mã minh họa chức năng thực thi ý định lệnh qua bộ phát lệnh (`AICommandIntent`, `AICommandDispatcher`)](images/chuong4/hinh-4-11.png)

---

### 4.3.4 Cấu hình độ khó và chế độ người chơi vs bot

Độ khó của bot được tinh chỉnh thông qua hai cấu hình chính là **`AIDifficultySO`** (chu kỳ quyết định `tickInterval`, ngưỡng tấn công/phòng thủ, mục tiêu kinh tế…) và **`AIGameSessionConfigSO`** (ánh xạ mức Dễ / Trung bình / Khó phòng chờ sang tài nguyên `AIDifficultySO` tương ứng). Thành phần **`PregameAiDifficultyApplicator`** áp dụng các thiết lập này khi vào scene Game 1 offline (Mirror không hoạt động), trước khi vòng lặp chiến lược bot chạy ổn định:

```csharp
// Assets/Scripts/AI/Config/AIDifficultySO.cs — tham số độ khó (một asset / mức)
[Header("Nhịp quyết định")]
[SerializeField] private float tickInterval = 0.65f;
[Header("Chiến thuật")]
[SerializeField] private float attackPowerThreshold = 1.2f;
[SerializeField] private int minArmyBeforeAttack = 6;

// Assets/Scripts/AI/Config/AIGameSessionConfigSO.cs
public AIDifficultySO Resolve(AIDifficultyLevel level)
{
    return level switch
    {
        AIDifficultyLevel.Easy => easy != null ? easy : medium,
        AIDifficultyLevel.Hard => hard != null ? hard : medium,
        _ => medium != null ? medium : easy
    };
}

// Assets/Scripts/PvAI/PregameAiDifficultyApplicator.cs
void Start()
{
    if (NetworkClient.active || NetworkServer.active || sessionConfig == null)
        return;

    AIController[] controllers = FindObjectsByType<AIController>(
        FindObjectsInactive.Include, FindObjectsSortMode.None);
    for (int i = 0; i < controllers.Length; i++)
        controllers[i].SetDifficulty(sessionConfig, PregameSessionState.SelectedDifficulty);
}

// Assets/Scripts/AI/Core/AIController.cs
public void SetDifficulty(AIGameSessionConfigSO session, AIDifficultyLevel level)
{
    if (session == null) return;
    SetDifficulty(session.Resolve(level)); // → tickInterval = profile.TickInterval
}
```

Trong chế độ **ngoại tuyến người chơi vs bot**, khi Mirror không hoạt động, **`PvAiGameSceneBootstrap`** gọi **`PvAiOfflineSpawnService.SpawnMatch()`** để khởi tạo trận. Quá trình này cấp phát Căn cứ chính và đơn vị ban đầu cho phe người chơi (`Player1`, spawn index 0) và phe bot (`AI2`, spawn index 1). `AIController` gắn sẵn scene với `aiOwner = AI2` đăng ký đơn vị phe bot qua `AIUnitRegistry` và chạy vòng tick chiến lược trong `Update()` sau khi spawn xong:

```csharp
// Assets/Scripts/PvAI/PvAiOfflineSpawnService.cs
public static void SpawnMatch(PvAiGameSceneSetup setup)
{
    if (setup.destroyScenePlacedCivilCentrals)
        RemoveScenePlacedCivilCentrals();

    SpawnFaction(setup, 0, setup.humanOwner);  // Owner.Player1
    SpawnFaction(setup, 1, setup.aiOwner);     // Owner.AI2
}

static void SpawnFaction(PvAiGameSceneSetup setup, int spawnIndex, Owner owner)
{
    Vector3 spawn = setup.factionSpawnPoints[spawnIndex].position;
    Quaternion rotation = setup.factionSpawnPoints[spawnIndex].rotation;

    GameObject civilCentral = PvAiOfflineEntityFactory.Spawn(
        setup.civilCentralPrefab, spawn, rotation, owner);

    if (setup.spawnStartingUnits)
        SpawnStartingUnits(setup, civilCentral, spawn, rotation, owner);
}

// Assets/Scripts/PvAI/PvAiGameSceneBootstrap.cs
void Start()
{
    if (s_spawnedThisSession || !ShouldRunOfflineSpawn())
        return;

    PvAiOfflineSpawnService.SpawnMatch(sceneSetup);
    s_spawnedThisSession = true;
}

bool ShouldRunOfflineSpawn()
{
    if (NetworkClient.active || NetworkServer.active)
        return false;
    return SceneManager.GetActiveScene().name.Contains(targetSceneName);
}
```

Cấu hình điểm xuất phát, prefab Căn cứ chính, `humanOwner`/`aiOwner` và danh sách đơn vị ban đầu nằm trên **`PvAiGameSceneSetup`** (Inspector scene Game 1).

**Hình 4.12. Mã minh họa chức năng áp độ khó bot (`PregameAiDifficultyApplicator`, `AIGameSessionConfigSO`)**

![Hình 4.12. Mã minh họa chức năng áp độ khó bot (`PregameAiDifficultyApplicator`, `AIGameSessionConfigSO`)](images/chuong4/hinh-4-12.png)

**Hình 4.13. Mã minh họa chức năng khởi tạo trận người chơi vs bot (`PvAiOfflineSpawnService.SpawnMatch`, `PvAiGameSceneBootstrap`)**

![Hình 4.13. Mã minh họa chức năng khởi tạo trận người chơi vs bot (`PvAiOfflineSpawnService.SpawnMatch`, `PvAiGameSceneBootstrap`)](images/chuong4/hinh-4-13.png)

---

## 4.4 Cài đặt nhận diện giọng nói

Hệ thống cho phép điều khiển bằng **lệnh thoại tiếng Việt** trong trận. Toàn bộ mã nằm trong `Assets/Scripts/SpeechRecognition/`; prefab tích hợp: `Assets/Prefab/SpeechRecognition.prefab`.

### 4.4.1 Tầng chuyển giọng nói thành văn bản

Tầng chuyển giọng nói thành văn bản (STT) được trừu tượng hóa thông qua giao diện **`ISpeechRecognitionBackend`**, chịu trách nhiệm nhận luồng âm thanh PCM 16-bit và phát các sự kiện văn bản tạm thời hoặc hoàn chỉnh. Triển khai thực tế sử dụng mô hình **Whisper** qua gói whisper.unity, bao gồm lớp **`WhisperSpeechRecognitionBackend`** cho luồng liên tục và **`WhisperSttOnlyDriver`** cho chế độ thu âm một lần (one-shot). Cấu hình hệ thống thiết lập ngôn ngữ tiếng Việt, bật tính năng phát hiện giọng nói (VAD) và sử dụng profile độ trễ thấp. Để hỗ trợ bộ giải mã nhận diện chính xác thuật ngữ RTS, **`WhisperVoicePromptBuilder`** sẽ sinh các câu gợi ý từ danh sách lệnh. Luồng thu âm one-shot do **`VoiceCommandOneShotCapture`** điều phối, giới hạn tối đa 12 giây, kiểm tra trạng thái mô hình trước khi ghi âm và xuất kết quả ra **`GameEventLog`**.

Mã minh họa — mở file `Assets/Scripts/SpeechRecognition/VoiceCommandOneShotCapture.cs`, chụp các hàm `BeginCaptureRoutine` và `FinishCapture`:

```csharp
// Assets/Scripts/SpeechRecognition/VoiceCommandOneShotCapture.cs
private IEnumerator BeginCaptureRoutine()
{
    // chờ Whisper InitModel / IsLoaded
    if (!_whisper.IsLoaded)
    {
        GameEventLog.Post("[Voice] Whisper chưa load xong …", GameEventLogCategory.Warning);
        yield break;
    }

    _isCapturing = true;
    GameEventLog.Post("[Voice] Đang nghe…", GameEventLogCategory.Info);
    _ = _sttDriver.StartListeningAsync();
    yield return CaptureTimeoutRoutine(); // tối đa _maxCaptureSeconds (12 giây)
}

private void FinishCapture(string transcript)
{
    LastTranscript = transcript ?? string.Empty;
    CancelCaptureInternal();

    if (LastTranscript.Length > 0)
        GameEventLog.Post($"[Voice] {LastTranscript}", GameEventLogCategory.Info);
    else
        GameEventLog.Post("[Voice] Không nghe được câu nào.", GameEventLogCategory.Warning);

    TranscriptCommitted?.Invoke(LastTranscript);
}
```

**Hình 4.14. Mã minh họa chức năng thu âm one-shot và chuyển giọng nói thành văn bản (`VoiceCommandOneShotCapture`)**

- **Tên file ảnh:** `hinh-4-14.png`
- **Đường dẫn lưu:** `docs/images/chuong4/hinh-4-14.png`
- **File chụp trong IDE:** `Assets/Scripts/SpeechRecognition/VoiceCommandOneShotCapture.cs` (khoảng dòng 114–186)

![Hình 4.14. Mã minh họa chức năng thu âm one-shot và chuyển giọng nói thành văn bản (`VoiceCommandOneShotCapture`)](images/chuong4/hinh-4-14.png)

---

### 4.4.2 Tập lệnh thoại và so khớp mờ

Mỗi lệnh thoại là một **`VoiceCommandEntry`** bao gồm mã lệnh, câu mẫu chính và danh sách các biến thể (từ đồng nghĩa, viết tắt, không dấu). Dữ liệu này được lưu trong **`VoiceCommandProfile`** (`ScriptableObject`) hoặc nạp từ tệp JSON lúc chạy (`Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json`). Trước khi so khớp, **`RecognizedSpeechPhraseNormalizer`** chuẩn hóa văn bản đầu vào: loại bỏ dấu câu, loại bỏ dấu tiếng Việt và chuyển sang chữ thường nhằm tăng tỷ lệ khớp (ví dụ: hệ thống hiểu *"dung lai"* tương đương *"dừng lại"*). Quá trình so khớp mờ được **`FuzzyVoiceCommandResolver`** thực hiện bằng thuật toán khoảng cách **Levenshtein**. Hệ thống sử dụng hai ngưỡng cấu hình: ngưỡng bám (**0,58**) để nhận diện câu nói thuộc về một mẫu, và ngưỡng kích hoạt (**0,72**) để chính thức xác nhận lệnh hợp lệ.

Mã minh họa — mở file `Assets/Scripts/SpeechRecognition/Core/RecognizedSpeechPhraseMapper.cs`, chụp hàm `TryMapToCanonicalPhrase`:

```csharp
// Assets/Scripts/SpeechRecognition/Core/RecognizedSpeechPhraseMapper.cs
public static bool TryMapToCanonicalPhrase(
    VoiceCommandProfile profile,
    IReadOnlyList<(string CommandId, string PhraseNormalized)> candidates,
    string recognizedText,
    out string commandId,
    out string canonicalPhraseInDatasetForm,
    out float similarity01,
    out bool isCommandMatch)
{
    var input = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(recognizedText);

    var bestSim = 0f;
    string bestId = null;
    foreach (var (cid, phrase) in candidates)
    {
        var sim = StringSimilarity.NormalizedSimilarity(input, phrase);
        if (sim > bestSim) { bestSim = sim; bestId = cid; }
    }

    if (bestId == null || bestSim < profile.PhraseSnapMinSimilarity) // ngưỡng bám ≈ 0,58
        return false;

    commandId = bestId;
    similarity01 = bestSim;
    isCommandMatch = bestSim >= profile.MinSimilarity; // ngưỡng kích hoạt ≈ 0,72
    return true;
}
```

**Hình 4.15. Mã minh họa chức năng so khớp mờ lệnh thoại (`RecognizedSpeechPhraseMapper`, Levenshtein)**

- **Tên file ảnh:** `hinh-4-15.png`
- **Đường dẫn lưu:** `docs/images/chuong4/hinh-4-15.png`
- **File chụp trong IDE:** `Assets/Scripts/SpeechRecognition/Core/RecognizedSpeechPhraseMapper.cs` (khoảng dòng 14–65)

![Hình 4.15. Mã minh họa chức năng so khớp mờ lệnh thoại (`RecognizedSpeechPhraseMapper`, Levenshtein)](images/chuong4/hinh-4-15.png)

---

### 4.4.3 Nhấn phím và ánh xạ lệnh gameplay

Quá trình thu âm bắt đầu khi người chơi nhấn phím **V**, được ghi nhận bởi **`VoiceCommandPushToTalkInput`** thông qua Unity Input System. Thao tác này kích hoạt **`VoiceCommandOneShotCapture`** và tự động đính kèm các thành phần xử lý phụ trợ nếu prefab chưa có. Bộ ánh xạ **`VoiceCommandOneShotTranscriptMapper`** sẽ tiếp nhận văn bản hoàn chỉnh, tiến hành chuẩn hóa và gọi bộ so khớp. Nếu kết quả đạt ngưỡng kích hoạt, hệ thống phát các sự kiện **`UnityEvent`** chứa mã lệnh. Các sự kiện này được nối trực tiếp trong Inspector tới các hàm xử lý gameplay (chọn lính, tấn công, dừng) mà không yêu cầu can thiệp mã nguồn STT. Nếu câu lệnh không khớp, hệ thống ghi log dưới dạng cảnh báo.

Mã minh họa — chụp **hai file** (có thể ghép một ảnh hoặc chụp liên tiếp trong IDE):

```csharp
// Assets/Scripts/SpeechRecognition/VoiceCommandPushToTalkInput.cs
void Update()
{
    if (_capture == null || !InputSystemKeyboardUtility.WasPressedThisFrame(captureKey))
        return;

    if (!_capture.IsCapturing)
        _capture.BeginCapture();
}

// Assets/Scripts/SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs
private void OnTranscriptCommitted(string transcript)
{
    string normalized = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(transcript);

    if (!_resolver.TryMapToCanonicalPhrase(
            normalized, out string commandId, out string canonicalPhrase,
            out float similarity, out bool isCommandMatch)
        || !isCommandMatch)
    {
        GameEventLog.Post($"[VoiceCmd] Không khớp lệnh: {normalized}", GameEventLogCategory.Warning);
        return;
    }

    _onCommandMatched?.Invoke(commandId);
    GameEventLog.Post($"[VoiceCmd] {commandId} ({similarity:0.00})", GameEventLogCategory.Info);
}
```

**Hình 4.16. Mã minh họa chức năng nhấn phím V và ánh xạ lệnh gameplay (`VoiceCommandPushToTalkInput`, `VoiceCommandOneShotTranscriptMapper`)**

- **Tên file ảnh:** `hinh-4-16.png`
- **Đường dẫn lưu:** `docs/images/chuong4/hinh-4-16.png`
- **File chụp trong IDE:**
  - `Assets/Scripts/SpeechRecognition/VoiceCommandPushToTalkInput.cs` (khoảng dòng 33–44)
  - `Assets/Scripts/SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs` (khoảng dòng 79–111)

![Hình 4.16. Mã minh họa chức năng nhấn phím V và ánh xạ lệnh gameplay (`VoiceCommandPushToTalkInput`, `VoiceCommandOneShotTranscriptMapper`)](images/chuong4/hinh-4-16.png)

**Bảng ánh xạ đầy đủ 67 lệnh:** xem file [`docs/bang-anh-xa-lenh-giong-noi.md`](bang-anh-xa-lenh-giong-noi.md) (STT, `CommandId`, câu mẫu, mô tả gameplay, biến thể nhận diện).

---

## 4.5 Kết luận và kiến nghị

### Kết luận

Đồ án *Phát triển trò chơi chiến thuật thời gian thực tích hợp điều khiển bằng giọng nói* đã **hoàn thiện và vận hành được** trên ba chế độ chơi: **đơn**, **đấu máy (PvE)** và **mạng nội bộ hai người (LAN)**. Sản phẩm đáp ứng mục tiêu đồ án: người chơi có thể trải nghiệm một ván RTS đầy đủ — thu thập tài nguyên, xây dựng, sản xuất quân, chiến đấu và (tùy chọn) ra lệnh bằng giọng nói tiếng Việt.

**Nền tảng gameplay.** Hệ thống cốt lõi xây dựng trên kiến trúc **Command Pattern**, **Event Bus** phân lập theo phe sở hữu và **Behavior Graph** điều khiển hành vi đơn vị. Các chức năng đã hiện thực và chạy ổn định gồm: quản lý kinh tế ba loại tài nguyên, xây dựng công trình, hàng đợi sản xuất đơn vị, vòng lặp chiến đấu, cây công nghệ, sương mù chiến tranh, bản đồ thu nhỏ và di chuyển theo đội hình khối vuông.

**Nhận diện giọng nói.** Phân hệ lệnh thoại tiếng Việt tích hợp vào scene chơi chính, hoạt động **ngoại tuyến** trên máy người chơi qua **Whisper** (whisper.unity) và lớp trừu tượng `ISpeechRecognitionBackend`. Người chơi nhấn phím **V**, nói câu lệnh; hệ thống chuyển giọng nói thành văn bản, **chuẩn hóa** và **so khớp mờ** (Levenshtein) với tập **35 ý định lệnh** trong `voice-command-dataset.vi.json`, sau đó ánh xạ sang `commandId` và kích hoạt lệnh gameplay tương ứng. Phản hồi hiển thị trên `GameEventLog` ngay trong trận.

**Trí tuệ nhân tạo (PvE).** Chế độ người chơi đấu bot triển khai qua **`AIController`**: bộ lập kế hoạch theo nhịp (tick planner), hàng đợi ưu tiên và ba bộ quản lý — kinh tế, căn cứ, quân sự. Bot tự thu thập, xây dựng, huấn luyện và tấn công; độ khó cấu hình qua `AIDifficultySO`. Luồng khởi tạo trận PvAI (`PvAiOfflineSpawnService`) sinh căn cứ và đơn vị ban đầu cho cả hai phe.

**Chơi mạng LAN.** Hai người chơi trong cùng mạng LAN tạo phòng, xác thực tên duy nhất, vào scene trận và **hoàn thành một ván đấu đầy đủ** qua **Mirror Networking** (Telepathy). Mô hình **server-authoritative** đảm bảo lệnh di chuyển, tấn công và thay đổi trạng thái quan trọng do máy chủ xác nhận trước khi đồng bộ; lớp cầu nối `RtsUts*` tích hợp gameplay UTS với Mirror mà không làm lõi game phụ thuộc trực tiếp vào API mạng.

Chương 4 (mục 4.2–4.4) minh họa bằng mã nguồn ba phân hệ mở rộng:

| Mục | Nội dung triển khai | Kết quả đạt được |
|-----|---------------------|------------------|
| **4.2 Chơi mạng** | Mirror, server-authoritative, Telepathy, `[Command]`/`SyncVar`, spawn và đồng bộ scene | Hai người chơi LAN hoàn thành ván RTS; lệnh unit đồng bộ qua máy chủ |
| **4.3 AI** | `AIController`, 3 bộ hoạch định, hàng đợi ưu tiên, `AICommandDispatcher` | Bot PvE tự vận hành kinh tế–quân sự; nhiều mức độ khó |
| **4.4 Giọng nói** | Whisper one-shot, dataset JSON, fuzzy hai ngưỡng, ánh xạ `commandId` | Điều khiển bằng giọng nói tiếng Việt trong trận |

Các module mở rộng (mạng, AI, giọng nói) được tách lớp rõ ràng, cấu hình qua `ScriptableObject` và JSON; kiến trúc thuận lợi cho bảo trì và mở rộng nội dung sau này.

---

### Kiến nghị và hướng phát triển

Trên nền hệ thống đã hoàn thiện, có thể mở rộng theo các hướng sau:

**Ngắn hạn — tinh chỉnh trải nghiệm người dùng**

- Bổ sung thêm biến thể câu lệnh và alias trong dataset giọng nói; tinh chỉnh ngưỡng fuzzy theo kết quả kiểm thử thực tế.
- Cải thiện giao diện phản hồi mic (đang thu / đã khớp lệnh) và thống kê độ trễ STT trên cấu hình máy mục tiêu.
- Cân bằng thêm chỉ số đơn vị và độ khó AI theo phản hồi người chơi thử nghiệm.

**Trung hạn — mở rộng tính năng hệ thống**

- Phát triển **trích xuất tham số động** (slot-filling) cho lệnh giọng nói: địa điểm, loại đơn vị, số lượng — nâng cao mức độ thay thế thao tác chuột.
- Tối ưu đồng bộ sương mù chiến tranh theo từng client trong chế độ mạng khi quy mô bản đồ và số người chơi tăng.
- Chuẩn hóa pipeline build tự động đóng gói model Whisper, dataset lệnh và asset voice cho bản phát hành.

**Dài hạn — mở rộng quy mô sản phẩm**

- Thử nghiệm **mô hình ngôn ngữ on-device** hoặc parser ngữ nghĩa để hiểu câu lệnh tự nhiên ngoài kịch bản cố định.
- Hỗ trợ **đa ngôn ngữ**; xây dựng hạ tầng **ghép trận trực tuyến**, lưu replay và chế độ spectator.
- Mở rộng nội dung (công trình, đơn vị, chiến dịch); phát triển phiên bản thương mại hoặc phân phối rộng hơn ngoài phạm vi LAN nội bộ.

Sơ đồ kiến trúc tổng thể, kết quả kiểm thử chức năng và minh chứng chơi thử (solo, PvE, LAN, lệnh thoại) được trình bày bổ sung trong báo cáo Word kèm theo.

---

## Phụ lục — Đường dẫn mã nguồn tham chiếu

| Tiểu mục | File chính |
|----------|------------|
| 4.2.1 | `Assets/3rdParty/RTS_Multiplayer/Scripts/RtsNetworkManager.cs` |
| 4.2.2 | `Assets/3rdParty/RTS_Multiplayer/Scripts/RtsUniqueNameAuthenticator.cs` |
| 4.2.3 | `Assets/Scripts/Netplay/RtsUtsClientCommandRelay.cs`, `RtsUtsPlayerCommands.cs`, `RtsUtsNetworkEntity.cs` |
| 4.2.4 | `Assets/Scripts/Netplay/RtsUtsServerSpawnHandler.cs`, `OwnerTeamMapping.cs` |
| 4.3.1 | `Assets/Scripts/AI/Core/AIController.cs`, `Assets/Scripts/AI/State/AIWorldState.cs` |
| 4.3.2 | `Assets/Scripts/AI/Managers/AIBaseManager.cs`, `AIMilitaryManager.cs`, `AIEconomyManager.cs` |
| 4.3.3 | `Assets/Scripts/AI/Core/AICommandIntent.cs`, `AICommandDispatcher.cs` |
| 4.3.4 | `AIDifficultySO.cs`, `AIGameSessionConfigSO.cs`, `PregameAiDifficultyApplicator.cs`, `PvAiOfflineSpawnService.cs`, `PvAiGameSceneBootstrap.cs` |
| 4.4.1 | `VoiceCommandOneShotCapture.cs`, `ISpeechRecognitionBackend.cs`, `WhisperSpeechRecognitionBackend.cs` |
| 4.4.2 | `RecognizedSpeechPhraseMapper.cs`, `VoiceCommandProfile.cs`, `FuzzyVoiceCommandResolver.cs` |
| 4.4.3 | `VoiceCommandPushToTalkInput.cs`, `VoiceCommandOneShotTranscriptMapper.cs` |
| Bảng lệnh giọng nói | [`docs/bang-anh-xa-lenh-giong-noi.md`](bang-anh-xa-lenh-giong-noi.md), `Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json` |

**Thư mục ảnh minh họa:** `docs/images/chuong4/`

| Hình | Tên file | Chú thích (Word) |
|------|----------|------------------|
| 4.14 | `hinh-4-14.png` | Mã minh họa chức năng thu âm one-shot và chuyển giọng nói thành văn bản (`VoiceCommandOneShotCapture`) |
| 4.15 | `hinh-4-15.png` | Mã minh họa chức năng so khớp mờ lệnh thoại (`RecognizedSpeechPhraseMapper`, Levenshtein) |
| 4.16 | `hinh-4-16.png` | Mã minh họa chức năng nhấn phím V và ánh xạ lệnh gameplay (`VoiceCommandPushToTalkInput`, `VoiceCommandOneShotTranscriptMapper`) |

*(Hình 4.5–4.13: mục 4.2–4.3; Hình 4.1–4.4: mục 4.1 trong Word.)*
