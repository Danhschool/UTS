# Assets/Scripts — Danh sách file theo module chức năng

> **Cập nhật:** 2026-06-04  
> **Phạm vi:** 456 file `.cs` trong `Assets/Scripts/`  
> **Đường dẫn gốc:** `Assets/Scripts/`

Mỗi module gồm: **mục tiêu**, **thứ tự đọc** (★), **subsection** (đúng cấu trúc module), **danh sách file** — mỗi dòng mô tả **vai trò file trong module/subsection đó** (cùng file có thể khác mô tả ở module khác).

**Định dạng:** `- **path** — Trong [module → subsection]: …` + chi tiết kỹ thuật (XML `///` hoặc SRP).

---

## Mục lục


| Module                                                          | Chức năng                            | Số file   |
| --------------------------------------------------------------- | ------------------------------------ | --------- |
| [M0 — Foundation](#m0--foundation-nền-tảng-chung)               | Owner, EventBus, Command/Unit base   | 12        |
| [M1 — Khởi động & scene](#m1--khởi-động-game--chuyển-scene)     | Menu → Loading → map → unlock        | 19        |
| [M2 — Input & ra lệnh](#m2--chọn-unit--ra-lệnh-player-input)    | Chuột, camera, command pipeline      | 59        |
| [M3 — Kinh tế & gather](#m3--kinh-tế--thu-thập-tài-nguyên)      | Supplies, worker gather              | 18        |
| [M4 — Xây dựng & sản xuất](#m4--xây-dựng--sản-xuất-unit)        | Build, queue, restrictions           | 16        |
| [M5 — Chiến đấu](#m5--chiến-đấu)                                | Attack, death, projectile            | 22        |
| [M6 — Fog of War](#m6--fog-of-war--tầm-nhìn)                    | Explored, ẩn/hiện unit               | 22        |
| [M7 — Minimap](#m7--minimap)                                    | Icon, click camera                   | 12        |
| [M8 — Vận tải](#m8--vận-tải-transport)                          | Load/unload unit                     | 11        |
| [M14 — Tech tree](#m14--tech-tree--upgrade)                     | Research, modifier                   | 7         |
| [M17 — Behavior Tree bridge](#m17--behavior-tree-bridge)        | Actions/conditions cho BT            | 32        |
| [M9 — Hotkeys](#m9--hotkeys)                                    | Phím tắt                             | 35        |
| [M10 — Lệnh giọng nói](#m10--lệnh-giọng-nói-speech-recognition) | STT → fuzzy → gameplay               | 27 + JSON |
| [M11 — UI](#m11--ui-in-game--menu)                              | Action bar, pregame, event log       | 50        |
| [M12 — Audio](#m12--audio)                                      | Nhạc, SFX, volume                    | 18        |
| [M13 — AI đối thủ](#m13--ai-đối-thủ)                            | Tick economy/base/military           | 71        |
| [M15 — Multiplayer](#m15--multiplayer-netplay)                  | Mirror, spawn MP                     | 14        |
| [M16 — PvAI offline](#m16--pvai-offline)                        | Spawn + AI không Mirror              | 13        |
| [Phụ trợ chéo module](#phụ-trợ-chéo-module-utilities--khác)     | Utilities, MapTools, Gameplay marker | 23        |


---

## M0 — Foundation (nền tảng chung)

**Mục tiêu:** Ngôn ngữ chung — phe (`Owner`), event (`Bus<T>`), lệnh (`ICommand`/`BaseCommand`), thực thể (`AbstractCommandable`).

**Phụ thuộc:** Không (đọc trước mọi module).

### Thứ tự đọc gợi ý

1. ★ `Units/Owner.cs`
2. ★ `EventBus/IEvent.cs` → `EventBus/Bus.cs`
3. ★ `Commands/ICommand.cs` → `Commands/BaseCommand.cs` → `Commands/CommandContext.cs`
4. ★ `Units/AbstractCommandable.cs`
5. `Units/ISelectable.cs`, `IDamageable.cs`, `IMoveable.cs`, `IAttacker.cs`
6. `EventBus/SupplySO.cs`

### Danh sách file

```
- **`Units/Owner.cs`** - Trong nền tảng chung (đọc trước mọi module): Enum phe trong game (Player1, Player2, AI2–AI7, Unowned…) — dùng trên unit, Bus event, fog.
- **`EventBus/IEvent.cs`** - Trong nền tảng chung (đọc trước mọi module): Marker interface — mọi payload event phải implement để dùng Bus<T>.
- **`EventBus/Bus.cs`** - Trong nền tảng chung (đọc trước mọi module): Event bus static generic theo Owner — đăng ký delegate và Raise(event) tách từng phe.
- **`EventBus/SupplySO.cs`** - Trong nền tảng chung (đọc trước mọi module): ScriptableObject loại tài nguyên (Food, Wood, Stone…) — icon, tên hiển thị.
- **`Commands/ICommand.cs`** - Trong nền tảng chung (đọc trước mọi module): Interface lệnh — CanHandle(CommandContext) và Handle(context); triển khai bởi BaseCommand SO.
- **`Commands/BaseCommand.cs`** - Trong nền tảng chung (đọc trước mọi module): Abstract ScriptableObject lệnh — slot UI, icon, ghost prefab, restrictions đặt nhà.
- **`Commands/CommandContext.cs`** - Trong nền tảng chung (đọc trước mọi module): Context thực thi lệnh: Owner local, unit đích, RaycastHit, danh sách selection.
- **`Units/AbstractCommandable.cs`** - Trong nền tảng chung (đọc trước mọi module): Base MonoBehaviour cho unit/nhà — health, owner, commands, fog visibility, selection decal.
- **`Units/ISelectable.cs`** - Trong nền tảng chung (đọc trước mọi module): Interface chọn được — IsSelected, Transform cho box select và UI.
- **`Units/IDamageable.cs`** - Trong nền tảng chung (đọc trước mọi module): Interface nhận sát thương — CurrentHealth, MaxHealth, OnHealthUpdated.
- **`Units/IMoveable.cs`** - Trong nền tảng chung (đọc trước mọi module): Interface di chuyển — API MoveTo cho NavMesh/formation.
- **`Units/IAttacker.cs`** - Trong nền tảng chung (đọc trước mọi module): Interface tấn công — damage, range, target acquisition.
```

---

## M1 — Khởi động game & chuyển scene

**Mục tiêu:** `MainMenu` → `SSScene` → `Loading` (Phase A) → map → Phase B (`GameplayStartupGate.Unlock`).

**Phụ thuộc:** M0.

### Thứ tự đọc gợi ý

1. ★ `Game/Pregame/PregameMenuSceneNavigator.cs`
2. ★ `Game/Startup/GameplayStartupScenes.cs`
3. ★ `Game/Startup/GameplaySceneLoader.cs`
4. ★ `Game/Startup/GameplayLoadingSceneController.cs`
5. ★ `Game/Startup/GameplayInGameReadyController.cs`
6. ★ `Game/Startup/GameplayStartupReadiness.cs`
7. ★ `Game/Startup/GameplayStartupGate.cs`
8. `Game/Startup/GameplaySceneLoaderHost.cs`, `GameplayLoadingSceneView.cs`
9. `Game/GameSpeedController.cs`, `GamePauseService.cs`, `MatchOutcomeFlow.cs`

### Danh sách file — `Game/` (19)

```
- **`Game/GameMatchOverlayStateSync.cs`** - Trong M1 khởi động & scene: SRP: Đồng bộ overlay in-game (tốc độ, pause, panel, đầu hàng) cho cả hai client MP. Settings chỉ local — không gửi qua lớp này.
- **`Game/GamePauseService.cs`** - Trong M1 khởi động & scene: SRP: Tạm dừng simulation in-game (Time.timeScale) — tách pause chia sẻ MP và pause local (settings).
- **`Game/GameSpeedController.cs`** - Trong M1 khởi động & scene: SRP: Điều khiển tốc độ simulation (1x / 2x) qua Time.timeScale.
- **`Game/MatchOutcomeFlow.cs`** - Trong M1 khởi động & scene: SRP: Chuyển scene khi trận kết thúc (đầu hàng / thua).
- **`Game/Pregame/PregameApplicationQuit.cs`** - Trong M1 → menu / setup (trước Loading): SRP: Thoát game / dừng Play mode trong Editor.
- **`Game/Pregame/PregameMenuSceneNavigator.cs`** - Trong M1 → menu / setup (trước Loading): SRP: Chuyển scene menu / setup — không đi qua Loading (chỉ gameplay mới qua Loading).
- **`Game/Pregame/PregamePlayMode.cs`** - Trong M1 → menu / setup (trước Loading): Enum chế độ pregame (Offline PvAI, Multiplayer host/client…) — lưu trong PregameSessionState.
- **`Game/Pregame/PregameSessionState.cs`** - Trong M1 → menu / setup (trước Loading): SRP: Lưu lựa chọn từ menu / màn setup trước khi vào Loading → gameplay.
- **`Game/Startup/Editor/GameplayLoadingSceneSetupEditor.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Tạo Loading.unity và cập nhật Build Settings cho luồng load scene.
- **`Game/Startup/GameplayInGameReadyController.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Phase B — trong scene game: chờ owner + Civil Central, bar 85%→100%, mở input/audio.
- **`Game/Startup/GameplayLoadingSceneController.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Phase A — Loading scene: async load file (offline) hoặc chờ host chuyển scene (MP).
- **`Game/Startup/GameplayLoadingSceneView.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: UI loading scene — fill bar Image (không dùng ProgressBar mask).
- **`Game/Startup/GameplaySceneLoader.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: API chuyển scene gameplay qua Loading scene (Phase A — load file / chờ MP).
- **`Game/Startup/GameplaySceneLoaderHost.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Host DontDestroyOnLoad — coroutine loading sống sót khi LoadSceneAsync(Single) hủy Loading scene.
- **`Game/Startup/GameplaySceneLoadRequest.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Gắn lên UI MainMenu / lobby offline — gọi GameplaySceneLoader.RequestLoad.
- **`Game/Startup/GameplayStartupGate.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Cổng "gameplay đã mở" — input/audio/UI gameplay chỉ chạy khi unlocked. Không đụng Time.timeScale.
- **`Game/Startup/GameplayStartupReadiness.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Kiểm tra client/local đã sẵn sàng chơi (owner + Civil Central).
- **`Game/Startup/GameplayStartupScenes.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: SRP: Nhận diện scene gameplay (Game*, RtsNet_Game, …) — không áp loading lên menu/lobby.
- **`Game/Startup/RtsNetworkSceneLoadHooksRegistration.cs`** - Trong M1 → Loading Phase A/B, cổng GameplayStartupGate: Type RtsNetworkSceneLoadHooksRegistration.
```

---

## M2 — Chọn unit & ra lệnh (Player Input)

**Mục tiêu:** Camera, box select, ghost đặt nhà, phát `BaseCommand` qua `PlayerInput`.

**Phụ thuộc:** M0, M1 (`GameplayStartupGate`).

### Thứ tự đọc gợi ý

1. ★ `Player/LocalHumanOwnerService.cs`
2. ★ `Player/PlayerInput.cs`
3. `Player/PlayerInputHotkeyIntegration.cs`, `Player/PlayerInput.Voice.cs`
4. ★ `Commands/AvailableCommandsResolver.cs`
5. ★ `Commands/MoveCommand.cs`, `AttackCommand.cs`, `StopCommand.cs`
6. ★ `Units/AbstractUnit.cs`
7. `Movement/MovementCursor.cs`

### Danh sách file — `Player/` (38)

```
- **`Player/CameraConfig.cs`** - Trong M2 → owner local, camera, PlayerInput: Type CameraConfig.
- **`Player/FactionFogPresentation.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Bật/tắt một cặp camera fog (explored + visibility) cho Player1 hoặc Player2.
- **`Player/FactionFogQuery.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: API tầm nhìn cho AI và gameplay — map viewer → fog faction tương ứng.
- **`Player/FactionFogSystemReference.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Holder camera + RT fog cho một human owner; đăng ký FactionFogSystemsRegistry.
- **`Player/FactionFogSystemsBootstrap.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Đảm bảo mọi FactionFogSystemReference trong scene đăng ký registry khi load.
- **`Player/FactionFogSystemsRegistry.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Registry Owner human → IFogMapQuery (không Find mỗi frame).
- **`Player/FactionHudBinder.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Gắn HUD / minimap / event log theo ILocalHumanOwner.LocalOwner.
- **`Player/FactionVisibilityUpdater.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Đọc vision RT của local human và cập nhật IHideable (không phải phe local).
- **`Player/FogCpuTextureMirror.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Mirror RenderTexture fog sang Texture2D CPU — ReadPixels có throttle, sample bilinear.
- **`Player/FogLocalOwnerDebugHotkeys.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Phím tắt debug đổi local human owner khi test fog P1/P2 (Editor / Development Build).
- **`Player/FogOrthographicUvUtility.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Map world XZ → UV texture fog ortho (dùng chung minimap, visibility, IFogMapQuery).
- **`Player/FogRenderTextureBootstrap.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Khởi tạo RT fog (explored/vision) về đen một lần mỗi texture — vùng chưa khám phá hiện đúng trên plane.
- **`Player/FogVisibilityManager.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: Giữ tên script trên scene/prefab cũ; logic nằm ở FactionVisibilityUpdater.
- **`Player/GameplayFogOverlayCamera.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Camera con trên Main Camera — chỉ đổi culling mask theo local owner (layer plane P1/P2), không sync mỗi frame.
- **`Player/GameplayWorldRaycastUtility.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Raycast world thống nhất cho chuột phải / hover cursor — tránh floor che supply (Move thay Gather).
- **`Player/HumanFogVisionUtility.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Quy tắc unit/building human nào phát tín hiệu fog vision trên client.
- **`Player/IFogMapQuery.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: ISP: Truy vấn explored/vision từ RT fog của một human faction.
- **`Player/IHideable.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: Type IHideable.
- **`Player/ILocalHumanOwner.cs`** - Trong M2 → owner local, camera, PlayerInput: ISP: Nguồn sự thật cho human player đang điều khiển trên client hiện tại.
- **`Player/LocalHumanCameraSpawnFocus.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Đặt cameraTarget tại spawn Civil Central của phe local (spawn point → vị trí CC khi replicate).
- **`Player/LocalHumanOwnerAccess.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Truy cập LocalOwner cho HUD/input mà không hardcode Player1.
- **`Player/LocalHumanOwnerBootstrap.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Gán Owner.Player1 khi chơi offline trên scene không có Mirror local player. Gắn trên Game 1 (hoặc scene single-player); bỏ qua khi Mirror client đang active.
- **`Player/LocalHumanOwnerMirrorBridge.cs`** - Trong M2 → owner local, camera, PlayerInput: DIP: Lobby Mirror → LocalHumanOwnerService + refresh presentation.
- **`Player/LocalHumanOwnerService.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Lưu và cung cấp ILocalHumanOwner.LocalOwner cho presentation/input trên client.
- **`Player/LocalHumanPresentationRefresh.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Điểm gọi refresh presentation — MP Director ưu tiên, offline dùng PlayerViewBinder.
- **`Player/MpHudSuppliesResolver.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Tìm component Supplies HUD đúng phe (Runtime UI UGUI vs (1)).
- **`Player/MpPlayerPresentationDirector.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: MP — mỗi client chỉ bật rig P1 hoặc P2 (fog + UI + PlayerInput riêng).
- **`Player/MpPlayerPresentationRig.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Một bộ presentation MP — fog + HUD + PlayerInput cho P1 hoặc P2 (gắn trên scene RtsNet_Game).
- **`Player/OwnerFogPlaneLayers.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: SRP: Map Owner → layer Unity cho Fog of War Plane (overlay Main Camera).
- **`Player/OwnerFogVisionLayers.cs`** - Trong M2 (phụ input) → visibility khi chọn/ra lệnh; chi tiết M6: Type OwnerFogVisionLayers.
- **`Player/Placeholder.cs`** - Trong M2 → owner local, camera, PlayerInput: Prefab marker tạm khi đặt building — preview vị trí trước khi spawn nhà thật.
- **`Player/PlayerInput.cs`** - Trong M2 → owner local, camera, PlayerInput: Điều khiển người chơi local: camera Cinemachine, box/chọn unit, raycast lệnh, ghost đặt nhà, formation, MP bridge.
- **`Player/PlayerInput.Voice.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Thực thi CommandId từ voice → selection / arm command / stop / gather.
- **`Player/PlayerInputHotkeyIntegration.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Gắn hotkey stack lên PlayerInput và bridge Esc/H vào gameplay. Tương thích MP: event bus theo LocalOwner; Stop relay qua PlayerInputNetworkBridge.
- **`Player/PlayerViewBinder.cs`** - Trong M2 → owner local, camera, PlayerInput: SRP: Orchestrator — bật đúng nhánh fog presentation theo ILocalHumanOwner.LocalOwner.
- **`Player/Supplies.cs`** - Trong M2 → owner local, camera, PlayerInput: Type Supplies.
- **`Player/SupplyAffordability.cs`** - Trong M2 → owner local, camera, PlayerInput: Kiểm tra đủ tài nguyên và đăng cảnh báo lên khung sự kiện (Player1).
- **`Player/UnitSelectionHoverCursor.cs`** - Trong M2 → owner local, camera, PlayerInput: Khi có ít nhất một AbstractUnit được chọn: raycast theo chuột trên world; nếu với <b>mọi</b> unit đã chọn, lệnh thắng khi chuột phải cùng là GatherCommand hoặc cùng là AttackCommand (cùng quy tắc thứ tự <c>CanHandle</c> như click phải) thì đổi cursor tương ứng — nhiều worker cùng khai thác vẫn thấy gather cursor. Vật có IHideable và <c>IsVisible == false</c> thì bỏ qua hit đó.
```

### Danh sách file — `Commands/` (21, dùng chung nhiều module)

```
- **`Commands/AttackCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Ra lệnh tấn công mục tiêu dưới con trỏ (sau selection).
- **`Commands/AvailableCommandsResolver.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Gom danh sách lệnh hiệu lực cho unit (override trước, rồi lệnh gốc), giống GameDevTV.RTS.Player.PlayerInput.
- **`Commands/BaseCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Abstract ScriptableObject lệnh — slot UI, icon, ghost prefab, restrictions đặt nhà.
- **`Commands/BuildBuildingCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/BuildingRestrictionSO.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: ScriptableObject cau hinh du lieu.
- **`Commands/BuildUnitCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/CancelBuildingCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/CommandContext.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Context thực thi lệnh: Owner local, unit đích, RaycastHit, danh sách selection.
- **`Commands/CommandFactionVisibility.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SRP: Tầm nhìn khi thực thi lệnh — human dùng fog UI; AI dùng FactionFogQuery (sight radius phe bot).
- **`Commands/CommandOwnershipUtility.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SRP: Xác nhận unit/building nhận lệnh trùng phe với CommandContext.Owner.
- **`Commands/CommandSupplyCostUtility.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Lấy chi phí / nhãn hành động từ các lệnh build & research (SRP cho cảnh báo tài nguyên).
- **`Commands/GatherCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Trong input: arm gather — Worker nhận lệnh trước khi BT loop (chi tiết M3).
- **`Commands/ICommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Interface lệnh — CanHandle(CommandContext) và Handle(context); triển khai bởi BaseCommand SO.
- **`Commands/IUnlockableCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/LoadIntoCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/LoadUnitCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/MoveCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Gán đích điểm/formation khi người chơi click bản đồ (sau khi chọn unit).
- **`Commands/OverrideCommandsCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/ResearchUpgradeCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
- **`Commands/StopCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: Dừng mọi hành động hiện tại của selection.
- **`Commands/UnloadAllUnitsCommand.cs`** - Trong M2 → lệnh kích hoạt từ click/selection: SO lenh CanHandle/Handle.
```

### Danh sách file — `Movement/` (1)

```
- **`Movement/MovementCursor.cs`** - Trong M2 → input chuột, camera, selection, phát lệnh: Positions the move marker and plays looped animation until the instance is destroyed.
```

---

## M3 — Kinh tế & thu thập tài nguyên

**Mục tiêu:** Food/gold/supply, worker gather, deposit.

**Phụ thuộc:** M0, M2, M17 (gather actions).

### Thứ tự đọc gợi ý

1. ★ `Player/Supplies.cs`, `SupplyAffordability.cs`
2. ★ `Environment/GatherableSupply.cs`, `IGatherable.cs`
3. ★ `Units/Worker.cs`
4. ★ `Commands/GatherCommand.cs`
5. M17: `GatherSuppliesEventChannel.cs`, `GatherSuppliesAction.cs`, `MoveToGatherableSupplyAction.cs`
6. `Utilities/SupplyDepositLocator.cs`, `SupplyDepositApproachUtility.cs`
7. Events: `SupplyEvent`, `SupplySpawnEvent`, `SupplyDepletedEvent`

### Danh sách file — `Player/` (kinh tế)

```
- **`Player/Supplies.cs`** - Trong M3 → kho tài nguyên & afford: MonoBehaviour kho phe — cộng/trừ Food/Wood/Stone, population; bind TMP UI.
- **`Player/SupplyAffordability.cs`** - Trong M3 → kho tài nguyên & afford: Kiểm tra đủ cost trước build/train; log cảnh báo lên GameEventLog.
```

### Danh sách file — `Commands/` (gather)

```
- **`Commands/GatherCommand.cs`** - Trong M3 → lệnh gather: Handle: gán worker tới mỏ/deposit hợp lệ; sau đó BT gather loop (M17).
```

### Danh sách file — `Environment/` (2)

```
- **`Environment/GatherableSupply.cs`** - Trong M3 → mỏ / thực thể gather: Type GatherableSupply.
- **`Environment/IGatherable.cs`** - Trong M3 → mỏ / thực thể gather: Type IGatherable.
```

### Danh sách file — Units / gather (trong `Units/`)

```
- **`Units/Worker.cs`** - Trong M3 → unit thực hiện gather: Unit kinh tế: nhận GatherCommand, gán event channel cho BT gather/return.
- **`Units/WorkerGatherAssignmentLock.cs`** - Trong M3 → unit thực hiện gather: Type WorkerGatherAssignmentLock.
- **`Units/WildAnimal.cs`** - Trong M3 → unit thực hiện gather: Unit động vật hoang: AI chỉ qua Behavior Graph (blackboard Command + Abort khi Command đổi).
- **`Units/AnimalAIConfigSO.cs`** - Trong M3 → unit thực hiện gather: ScriptableObject cau hinh du lieu.
```

### Danh sách file — Events liên quan

```
- **`Events/SupplyEvent.cs`** - Trong M3 → event Bus (tài nguyên): Payload Bus<T> theo Owner.
- **`Events/SupplySpawnEvent.cs`** - Trong M3 → event Bus (tài nguyên): Payload Bus<T> theo Owner.
- **`Events/SupplyDepletedEvent.cs`** - Trong M3 → event Bus (tài nguyên): Payload Bus<T> theo Owner.
```

### Danh sách file — Utilities liên quan

```
- **`Utilities/SupplyDepositLocator.cs`** - Trong M3 → helper gather/deposit: Locates completed buildings where workers can deposit gathered supplies.
- **`Utilities/SupplyDepositApproachUtility.cs`** - Trong M3 → helper gather/deposit: SRP: Điểm tiếp cận động quanh Store/Civil Central — mỗi worker một ô trên vòng quanh footprint.
- **`Utilities/StartingWorkerSpawnLayout.cs`** - Trong M3 → helper gather/deposit: SRP: Tính vị trí spawn worker khởi đầu quanh điểm base (PvE / MP dùng chung).
- **`Utilities/StartingUnitSpawnEntry.cs`** - Trong M3 → helper gather/deposit: SRP: Một dòng cấu hình spawn unit khởi đầu (prefab + số lượng).
```

### Danh sách file — Audio liên quan

```
- **`Audio/SupplyGatherAudioUtility.cs`** - Trong M3 → SFX gather: SRP: Map SupplySO → cue gather (stone / wood / food).
```

---

## M4 — Xây dựng & sản xuất unit

**Mục tiêu:** Đặt nhà, queue build, train unit, hủy công trình.

**Phụ thuộc:** M0, M2, M3, M11, M17.

### Thứ tự đọc gợi ý

1. ★ `Units/BaseBuilding.cs`, `BuildingProgress.cs`
2. ★ `Commands/BuildBuildingCommand.cs`, `BuildUnitCommand.cs`, `CancelBuildingCommand.cs`
3. `Commands/BuildingRestrictionSO.cs`, `CommandSupplyCostUtility.cs`
4. M17: `BuildBuildingAction.cs`, `BuildingEventChannel.cs`, `BuildingIsInProgressCondition.cs`
5. M11: `BuildingBuildingUI.cs`, `BuildingUnderConstructionUI.cs`, `UIBuildQueueButton.cs`

### Danh sách file — `Units/` (building)

```
- **`Units/BaseBuilding.cs`** - Trong M4 → UI queue & panel nhà: Type BaseBuilding.
- **`Units/BuildingProgress.cs`** - Trong M4 → UI queue & panel nhà: Type BuildingProgress.
- **`Units/BuildingSO.cs`** - Trong M4 → UI queue & panel nhà: ScriptableObject cau hinh du lieu.
- **`Units/BuildingEventType.cs`** - Trong M4 → UI queue & panel nhà: Type BuildingEventType.
- **`Units/IBuildingBuilder.cs`** - Trong M4 → UI queue & panel nhà: Type IBuildingBuilder.
- **`Units/IBuildingPassiveEffect.cs`** - Trong M4 → UI queue & panel nhà: Passive building behaviours toggled with BaseBuilding enable/disable.
- **`Units/Buildings/BuildingAutoAttack.cs`** - Trong M4 → UI queue & panel nhà: Automatically attacks the nearest hostile unit in range (owner different from this building). Spawns a projectile from an elevated fire point and fires straight down toward DamageableSensor.
- **`Units/Buildings/BuildingAutoAttackConfigSO.cs`** - Trong M4 → UI queue & panel nhà: ScriptableObject cau hinh du lieu.
- **`Units/Buildings/BuildingEffectUtility.cs`** - Trong M4 → UI queue & panel nhà: Shared checks for passive building effects (food gen, tower attack, …).
- **`Units/Buildings/PassiveFoodGeneratorBuilding.cs`** - Trong M4 → UI queue & panel nhà: Periodically adds food to the building owner's supply pool when the structure is operational.
- **`Units/Buildings/PassiveFoodGeneratorConfigSO.cs`** - Trong M4 → UI queue & panel nhà: ScriptableObject cau hinh du lieu.
- **`Units/Buildings/TowerDownwardProjectileFlight.cs`** - Trong M4 → UI queue & panel nhà: Tower projectile: parabolic arc from fire point to the aim position (DamageableSensor).
```

### Danh sách file — Commands (build)

```
- **`Commands/BuildBuildingCommand.cs`** - Trong M4 → lệnh Build/Cancel/Train: Ghost đặt nhà + spawn placeholder → BuildBuildingAction (M17).
- **`Commands/BuildUnitCommand.cs`** - Trong M4 → lệnh Build/Cancel/Train: Đưa unit vào queue sản xuất tại building có queue.
- **`Commands/CancelBuildingCommand.cs`** - Trong M4 → lệnh Build/Cancel/Train: SO lenh CanHandle/Handle.
- **`Commands/BuildingRestrictionSO.cs`** - Trong M4 → lệnh Build/Cancel/Train: ScriptableObject cau hinh du lieu.
- **`Commands/CommandSupplyCostUtility.cs`** - Trong M4 → lệnh Build/Cancel/Train: Lấy chi phí / nhãn hành động từ các lệnh build & research (SRP cho cảnh báo tài nguyên).
```

### Danh sách file — Events

```
- **`Events/BuildingConstructStartedEvent.cs`** - Trong M4 → event Bus (xây/spawn): Phát khi GameDevTV.RTS.Behavior.BuildBuildingAction bắt đầu (nhà được spawn / tiếp tục xây) — dùng để gỡ ghost đặt chỗ trên UI.
- **`Events/BuildingSpawnEvent.cs`** - Trong M4 → event Bus (xây/spawn): Payload Bus<T> theo Owner.
- **`Events/BuildingDeathEvent.cs`** - Trong M4 → event Bus (xây/spawn): Payload Bus<T> theo Owner.
- **`Events/PlaceholderSpawnEvent.cs`** - Trong M4 → event Bus (xây/spawn): Payload Bus<T> theo Owner.
- **`Events/PlaceholderDestroyEvent.cs`** - Trong M4 → event Bus (xây/spawn): Payload Bus<T> theo Owner.
```

### Danh sách file — Utilities

```
- **`Utilities/CivilCentralUtility.cs`** - Trong M4 → placement & Civil Central: Identifies the main base (Civil Central) for win/lose and supply-deposit rules.
- **`Utilities/BuildingKindMatching.cs`** - Trong M4 → placement & Civil Central: SRP: So khớp building với prefab archetype (hotkey A/S/D).
- **`Utilities/PlacementFieldGridContext.cs`** - Trong M4 → placement & Civil Central: Type PlacementFieldGridContext.
- **`Utilities/PlacementFieldGridUtility.cs`** - Trong M4 → placement & Civil Central: SRP: Ánh xạ tọa độ thế giới → chỉ số ô trên lưới placement field.
- **`Utilities/PlacementFieldSelectionRegistry.cs`** - Trong M4 → placement & Civil Central: SRP: AI — lưu địa chỉ đặt nhà đã chọn theo phe và tránh chọn trùng trong cùng ô field (bán kính tối thiểu). Người chơi dùng ghost + Restrictions trên Player.PlayerInput; không gọi registry này.
- **`Utilities/ClosestCommandPostComparer.cs`** - Trong M4 → placement & Civil Central: Type ClosestCommandPostComparer.
```

### Danh sách file — UI (building)

```
- **`UI/Containers/BuildingSelectedUI.cs`** - Trong M4 → UI queue & panel nhà: Type BuildingSelectedUI.
- **`UI/Containers/BuildingBuildingUI.cs`** - Trong M4 → UI queue & panel nhà: Type BuildingBuildingUI.
- **`UI/Containers/BuildingUnderConstructionUI.cs`** - Trong M4 → UI queue & panel nhà: Type BuildingUnderConstructionUI.
- **`UI/Components/UIBuildQueueButton.cs`** - Trong M4 → UI queue & panel nhà: Type UIBuildQueueButton.
```

---

## M5 — Chiến đấu

**Mục tiêu:** Tấn công, auto-attack, projectile, chết unit.

**Phụ thuộc:** M0, M2, M6 (visibility), M17.

### Thứ tự đọc gợi ý

1. ★ `Commands/AttackCommand.cs`
2. ★ `Units/BaseMilitaryUnit.cs`, `Archer.cs`, `Grenadier.cs`
3. `Units/Combat/`*
4. `Units/UnitDeathController.cs` + M17 `Behavior/Death/*`
5. `Audio/AttackAudioUtility.cs`

### Danh sách file — `Units/` (combat)

```
- **`Units/BaseMilitaryUnit.cs`** - Trong M5 → unit/building chiến đấu: Type BaseMilitaryUnit.
- **`Units/Archer.cs`** - Trong M5 → unit/building chiến đấu: Type Archer.
- **`Units/Grenadier.cs`** - Trong M5 → unit/building chiến đấu: Type Grenadier.
- **`Units/AttackConfigSO.cs`** - Trong M5 → unit/building chiến đấu: ScriptableObject cau hinh du lieu.
- **`Units/DamageableSensor.cs`** - Trong M5 → unit/building chiến đấu: Type DamageableSensor.
- **`Units/HomingArrowFlight.cs`** - Trong M5 → unit/building chiến đấu: Gắn trên Archer. Spawn prefab mũi tên; bay vòng cung hoặc homing ngang tùy cấu hình.
- **`Units/HoldGunIK.cs`** - Trong M5 → unit/building chiến đấu: Type HoldGunIK.
- **`Units/IProjectileAttacker.cs`** - Trong M5 → unit/building chiến đấu: Unit gây damage bằng projectile; Behavior.AttackTargetAction gọi khi AttackConfig.HasProjectileAttacks.
- **`Units/UnitDeathController.cs`** - Trong M5 → unit/building chiến đấu: API chết từng bước cho Behavior Graph (Cách A): prepare → animator → wait → freeze → hold → sink → destroy (node riêng).
- **`Units/UnitDeathConfigSO.cs`** - Trong M5 → unit/building chiến đấu: ScriptableObject cau hinh du lieu.
- **`Units/Combat/CombatAreaTargetQuery.cs`** - Trong M5 → unit/building chiến đấu: SRP: Tìm mục tiêu hostiles trong vùng tròn (rampage / sweep).
- **`Units/Combat/CombatTargetPriorityUtility.cs`** - Trong M5 → unit/building chiến đấu: SRP: Ưu tiên mục tiêu — lính địch trước công trình địch (cùng khoảng cách / trong vùng).
- **`Units/Combat/UnitAreaRampageController.cs`** - Trong M5 → unit/building chiến đấu: SRP: Phản công vùng khi bị đánh — quét hostile trong bán kính; hết địch thì tắt (AI/planner dùng Attack thường).
- **`Units/Combat/UnitCounterAttackUtility.cs`** - Trong M5 → unit/building chiến đấu: SRP: Kích hoạt phản công vùng khi lính bị địch đánh — không qua lệnh UI/AI.
- **`Units/Buildings/BuildingAutoAttack.cs`** - Trong M5 → unit/building chiến đấu: Automatically attacks the nearest hostile unit in range (owner different from this building). Spawns a projectile from an elevated fire point and fires straight down toward DamageableSensor.
- **`Units/Buildings/BuildingAutoAttackConfigSO.cs`** - Trong M5 → unit/building chiến đấu: ScriptableObject cau hinh du lieu.
- **`Units/Buildings/TowerDownwardProjectileFlight.cs`** - Trong M5 → unit/building chiến đấu: Tower projectile: parabolic arc from fire point to the aim position (DamageableSensor).
- **`Units/Visualization/AttackRangeCircleUtility.cs`** - Trong M5 → unit/building chiến đấu: Tạo điểm vòng tròn trên mặt phẳng XZ (tầm đánh trên map).
- **`Units/Visualization/AttackRangeDisplayInstaller.cs`** - Trong M5 → unit/building chiến đấu: Gắn UnitAttackRangeDisplay cho unit có tầm đánh (không áp dụng building).
- **`Units/Visualization/UnitAttackRangeDisplay.cs`** - Trong M5 → unit/building chiến đấu: Hiển thị vòng tròn tầm đánh khi unit được chọn (LineRenderer) và trong Scene view (Gizmos).
```

### Danh sách file — Commands / Events / Utilities / Audio

```
- **`Commands/AttackCommand.cs`** - Trong M5 → lệnh Attack: SO lenh CanHandle/Handle.
- **`Events/UnitDeathEvent.cs`** - Trong M5 → lệnh Attack: Payload Bus<T> theo Owner.
- **`Utilities/HostileTargetLocator.cs`** - Trong M5 → lệnh Attack: Finds hostile IDamageable targets in range for defensive buildings.
- **`Utilities/CombatTargetGeometryUtility.cs`** - Trong M5 → lệnh Attack: Resolves closest surface points on combat targets (colliders, NavMeshObstacle footprint).
- **`Utilities/DamageableSensorAimUtility.cs`** - Trong M5 → lệnh Attack: Resolves world aim points on units (DamageableSensor when present).
- **`Utilities/ProjectileArcMath.cs`** - Trong M5 → lệnh Attack: Shared parabolic arc math for unit/building projectiles.
- **`Audio/AttackAudioUtility.cs`** - Trong M5 → lệnh Attack: SRP: Chọn cue tấn công theo AttackConfigSO.
```

---

## M6 — Fog of War & tầm nhìn

**Mục tiêu:** Explored/unexplored, layer vision, ẩn unit địch.

**Phụ thuộc:** M0, M1, M7 (minimap fog).

### Thứ tự đọc gợi ý

1. ★ `Player/FactionFogSystemsBootstrap.cs`, `FactionFogSystemsRegistry.cs`
2. ★ `Player/HumanFogVisionUtility.cs`
3. ★ `Player/OwnerFogVisionLayers.cs`
4. `Player/FogCpuTextureMirror.cs`, `GameplayFogOverlayCamera.cs`
5. `Commands/CommandFactionVisibility.cs`
6. `Minimap/MinimapFogSystemReference.cs`, `MinimapExploredFogOverlay.cs`

### Danh sách file — `Player/` (fog)

```
- **`Player/FactionFogPresentation.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Bật/tắt một cặp camera fog (explored + visibility) cho Player1 hoặc Player2.
- **`Player/FactionFogQuery.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: API tầm nhìn cho AI và gameplay — map viewer → fog faction tương ứng.
- **`Player/FactionFogSystemReference.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Holder camera + RT fog cho một human owner; đăng ký FactionFogSystemsRegistry.
- **`Player/FactionFogSystemsBootstrap.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Đảm bảo mọi FactionFogSystemReference trong scene đăng ký registry khi load.
- **`Player/FactionFogSystemsRegistry.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Registry Owner human → IFogMapQuery (không Find mỗi frame).
- **`Player/FogCpuTextureMirror.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Mirror RenderTexture fog sang Texture2D CPU — ReadPixels có throttle, sample bilinear.
- **`Player/FogLocalOwnerDebugHotkeys.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Phím tắt debug đổi local human owner khi test fog P1/P2 (Editor / Development Build).
- **`Player/FogOrthographicUvUtility.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Map world XZ → UV texture fog ortho (dùng chung minimap, visibility, IFogMapQuery).
- **`Player/FogRenderTextureBootstrap.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Khởi tạo RT fog (explored/vision) về đen một lần mỗi texture — vùng chưa khám phá hiện đúng trên plane.
- **`Player/FogVisibilityManager.cs`** - Trong M6 → fog of war & tầm nhìn: Giữ tên script trên scene/prefab cũ; logic nằm ở FactionVisibilityUpdater.
- **`Player/GameplayFogOverlayCamera.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Camera con trên Main Camera — chỉ đổi culling mask theo local owner (layer plane P1/P2), không sync mỗi frame.
- **`Player/HumanFogVisionUtility.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Quy tắc unit/building human nào phát tín hiệu fog vision trên client.
- **`Player/IFogMapQuery.cs`** - Trong M6 → fog of war & tầm nhìn: ISP: Truy vấn explored/vision từ RT fog của một human faction.
- **`Player/OwnerFogPlaneLayers.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Map Owner → layer Unity cho Fog of War Plane (overlay Main Camera).
- **`Player/OwnerFogVisionLayers.cs`** - Trong M6 → fog of war & tầm nhìn: Type OwnerFogVisionLayers.
- **`Player/FactionVisibilityUpdater.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Đọc vision RT của local human và cập nhật IHideable (không phải phe local).
```

### Danh sách file — khác

```
- **`Commands/CommandFactionVisibility.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Tầm nhìn khi thực thi lệnh — human dùng fog UI; AI dùng FactionFogQuery (sight radius phe bot).
- **`Units/AbstractCommandable.cs`** - Trong M6 → fog of war & tầm nhìn: Base MonoBehaviour cho unit/nhà — health, owner, commands, fog visibility, selection decal.
- **`Netplay/MpFogRefreshThrottle.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Giới hạn tần suất refresh fog/visibility MP — tránh lag do FindObjects + full pass lặp.
- **`Netplay/MpFogVisionSpawnRefresh.cs`** - Trong M6 → fog of war & tầm nhìn: SRP: Sau khi unit/building MP replicate — refresh layer vision + fog plane texture cho local human.
```

*(File fog trong Minimap: xem [M7](#m7--minimap).)*

---

## M7 — Minimap

**Mục tiêu:** Icon unit/supply, click di chuyển camera, fog overlay.

**Phụ thuộc:** M2 (`IMinimapCameraNavigator`), M6.

### Thứ tự đọc gợi ý

1. ★ `Minimap/MinimapController.cs`
2. ★ `Minimap/MinimapInputHandler.cs`
3. `Minimap/MinimapRenderCamera.cs`
4. `Minimap/MinimapUnitIconsController.cs`, `MinimapSupplyIconsController.cs`

### Danh sách file — `Minimap/` (12)

```
- **`Minimap/IMinimapCameraNavigator.cs`** - Trong M7 → minimap: Type IMinimapCameraNavigator.
- **`Minimap/MinimapController.cs`** - Trong M7 → minimap: Gắn trên Minimap Container: hiển thị RT từ MinimapRenderCamera và click di chuyển camera.
- **`Minimap/MinimapExploredFogOverlay.cs`** - Trong M7 → minimap: Lớp UI fog minimap: đen (chưa explore), mờ (explored), trong suốt (đang nhìn thấy).
- **`Minimap/MinimapFogSystemReference.cs`** - Trong M7 → minimap: Tham chiếu camera/RT fog explored + vision dùng chung cho minimap overlay và icon.
- **`Minimap/MinimapIconStyleSO.cs`** - Trong M7 → minimap: ScriptableObject cau hinh du lieu.
- **`Minimap/MinimapIconView.cs`** - Trong M7 → minimap: Type MinimapIconView.
- **`Minimap/MinimapInputHandler.cs`** - Trong M7 → minimap: Handler phim tat.
- **`Minimap/MinimapMapBoundsSO.cs`** - Trong M7 → minimap: ScriptableObject cau hinh du lieu.
- **`Minimap/MinimapMarkerPresentation.cs`** - Trong M7 → minimap: Type MinimapMarkerPresentation.
- **`Minimap/MinimapRenderCamera.cs`** - Trong M7 → minimap: Camera ortho nhìn từ trên, render scene vào RT hiển thị trên minimap UI.
- **`Minimap/MinimapSupplyIconsController.cs`** - Trong M7 → minimap: Icon supply trên minimap: hiện khi đang trong vision; sau lần đầu mất vision thì luôn hiện.
- **`Minimap/MinimapUnitIconsController.cs`** - Trong M7 → minimap: Hiển thị icon unit/building trên minimap UI (overlay), dùng UnitSO.Icon — không gắn object 3D trên đầu unit.
```

---

## M8 — Vận tải (transport)

**Mục tiêu:** Load/unload unit vào transport.

**Phụ thuộc:** M0, M2, M17.

### Thứ tự đọc gợi ý

1. ★ `Units/ITransporter.cs`, `ITransportable.cs`, `AirTransport.cs`
2. ★ `Commands/LoadUnitCommand.cs`, `LoadIntoCommand.cs`, `UnloadAllUnitsCommand.cs`
3. M17: `LoadUnitEventChannel.cs`
4. `UI/Containers/UnitTransportUI.cs`

### Danh sách file

```
- **`Units/ITransporter.cs`** - Trong M8 → transport load/unload: Type ITransporter.
- **`Units/ITransportable.cs`** - Trong M8 → transport load/unload: Type ITransportable.
- **`Units/AirTransport.cs`** - Trong M8 → transport load/unload: Type AirTransport.
- **`Units/TransportConfigSO.cs`** - Trong M8 → transport load/unload: ScriptableObject cau hinh du lieu.
- **`Commands/LoadUnitCommand.cs`** - Trong M8 → transport load/unload: SO lenh CanHandle/Handle.
- **`Commands/LoadIntoCommand.cs`** - Trong M8 → transport load/unload: SO lenh CanHandle/Handle.
- **`Commands/UnloadAllUnitsCommand.cs`** - Trong M8 → transport load/unload: SO lenh CanHandle/Handle.
- **`Behavior/LoadUnitEventChannel.cs`** - Trong M8 → transport load/unload: Kenh event Behavior Graph.
- **`Events/UnitLoadEvent.cs`** - Trong M8 → transport load/unload: Payload Bus<T> theo Owner.
- **`Events/UnitUnloadEvent.cs`** - Trong M8 → transport load/unload: Payload Bus<T> theo Owner.
- **`UI/Containers/UnitTransportUI.cs`** - Trong M8 → transport load/unload: Type UnitTransportUI.
```

---

## M14 — Tech tree & upgrade

**Mục tiêu:** Research upgrade, modifier stats, unlock command.

**Phụ thuộc:** M0, M4, M11, Events.

### Thứ tự đọc gợi ý

1. ★ `TechTree/UpgradeSO.cs`, `TechTreeSO.cs`
2. ★ `Commands/ResearchUpgradeCommand.cs`
3. `Events/UpgradeResearchedEvent.cs`

### Danh sách file — `TechTree/` (7)

```
- **`TechTree/AdditiveFloatModifierSO.cs`** - Trong M14 → tech tree / upgrade: ScriptableObject cau hinh du lieu.
- **`TechTree/AdditiveIntModifierSO.cs`** - Trong M14 → tech tree / upgrade: ScriptableObject cau hinh du lieu.
- **`TechTree/IModifier.cs`** - Trong M14 → tech tree / upgrade: Type IModifier.
- **`TechTree/InvalidPathSpecifiedException.cs`** - Trong M14 → tech tree / upgrade: Type InvalidPathSpecifiedException.
- **`TechTree/TechTreeSO.cs`** - Trong M14 → tech tree / upgrade: ScriptableObject cau hinh du lieu.
- **`TechTree/UnlockableSO.cs`** - Trong M14 → tech tree / upgrade: ScriptableObject cau hinh du lieu.
- **`TechTree/UpgradeSO.cs`** - Trong M14 → tech tree / upgrade: ScriptableObject cau hinh du lieu.
```

### Commands / Events

```
- **`Commands/ResearchUpgradeCommand.cs`** - Trong M14 → tech tree / upgrade: SO lenh CanHandle/Handle.
- **`Commands/IUnlockableCommand.cs`** - Trong M14 → tech tree / upgrade: SO lenh CanHandle/Handle.
- **`Events/UpgradeResearchedEvent.cs`** - Trong M14 → tech tree / upgrade: Payload Bus<T> theo Owner.
```

---

## M17 — Behavior Tree bridge

**Mục tiêu:** Custom actions/conditions/event channels cho Unity Behavior Graph trên prefab.

**Phụ thuộc:** Gắn với M2–M5 (đọc song song graph `.asset` trong Editor).

**Graph assets (không nằm trong Scripts):** `Assets/Data_Re/Behavior Graph/`

### Thứ tự đọc gợi ý

1. ★ `Behavior/GatherSuppliesEventChannel.cs`, `BuildingEventChannel.cs`, `LoadUnitEventChannel.cs`
2. ★ `Behavior/MoveToTargetLocationAction.cs`, `AttackTargetAction.cs`, `GatherSuppliesAction.cs`, `BuildBuildingAction.cs`
3. `Behavior/StopAgentAction.cs`, `SetNavMeshAgentEnabledAction.cs`
4. `Behavior/Death/`*, `Behavior/Animal/*`

### Danh sách file — `Behavior/` (32)

```
- **`Behavior/Animal/AnimalEatAnimationAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Animal/AnimalIdleAnimationAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Animal/EvaluateAnimalAICommandAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Animal/SpawnCorpseFoodSupplyAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/AttackTargetAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/BuildBuildingAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/BuildingEventChannel.cs`** - Trong M17 → Behavior Tree bridge: Kenh event Behavior Graph.
- **`Behavior/BuildingIsInProgressCondition.cs`** - Trong M17 → Behavior Tree bridge: Type BuildingIsInProgressCondition.
- **`Behavior/Death/DeathBehaviorNodeUtility.cs`** - Trong M17 → Behavior Tree bridge: Static utility.
- **`Behavior/Death/DisableUnitDeathGameplayAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Death/FreezeUnitDeathPoseAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Death/HoldUnitDeathPoseAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Death/SetUnitDeathAnimatorAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Death/SinkUnitDownAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/Death/WaitUnitDeathAnimationAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/FindClosestCommandPostAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/GameObjectListSizeCondition.cs`** - Trong M17 → Behavior Tree bridge: Type GameObjectListSizeCondition.
- **`Behavior/GatherSuppliesAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/GatherSuppliesEventChannel.cs`** - Trong M17 → Behavior Tree bridge: Kenh event Behavior Graph.
- **`Behavior/LoadUnitEventChannel.cs`** - Trong M17 → Behavior Tree bridge: Kenh event Behavior Graph.
- **`Behavior/MoveToGatherableSupplyAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/MoveToTargetGameObjectAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/MoveToTargetLocationAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/PickClosestPointOnColliderAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/PickClosestPointOnTargetColliderAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/PickRandomLocationWithinRendererBoundsAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/SamplePositionAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/SetAgentAvoidanceAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/SetNavMeshAgentEnabledAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/SetTargetFromFirstObjectInListAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/StopAgentAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
- **`Behavior/TranslatePositionAction.cs`** - Trong M17 → Behavior Tree bridge: Node hanh dong Unity Behavior Graph.
```

---

## M9 — Hotkeys

**Mục tiêu:** Phím tắt thay thế click (camera, select type, stop, cancel…).

**Phụ thuộc:** M0, M1, M2.

### Thứ tự đọc gợi ý

1. ★ `Hotkeys/HotkeyService.cs` → `HotkeySystem.cs`
2. ★ `Hotkeys/HotkeyHandlerBase.cs`, `HotkeyContext.cs`
3. ★ `Hotkeys/LocalHumanHotkeyGate.cs`
4. `Hotkeys/Handlers/`* (từng handler = 1 chức năng)
5. `Hotkeys/Targets/IHotkey*.cs`

### Danh sách file — `Hotkeys/` (35)

```
- **`Hotkeys/BuildingTypeHotkeySetup.cs`** - Trong M9 → hotkey: SRP: Gắn prefab nhà → HotkeyId A/S/D và đăng ký delegate lên HotkeyService.
- **`Hotkeys/CompositeHotkeyGate.cs`** - Trong M9 → hotkey: ISP: Kết hợp nhiều gate nhỏ (UI focus, MP local owner…).
- **`Hotkeys/Handlers/ActionBarSlotHotkeyHandler.cs`** - Trong M9 → hotkey: Phím 1–9 → kích hoạt lệnh slot tương ứng trên unit đang chọn.
- **`Hotkeys/Handlers/CameraFollowUnitHotkeyHandler.cs`** - Trong M9 → hotkey: F → pan camera tới unit/nhà phe local đang chọn.
- **`Hotkeys/Handlers/CameraResetHotkeyHandler.cs`** - Trong M9 → hotkey: Home → reset zoom / góc camera.
- **`Hotkeys/Handlers/CancelSelectionHotkeyHandler.cs`** - Trong M9 → hotkey: Esc → gọi IHotkeyCancelTarget. Mẫu handler mở rộng theo từng HotkeyId.
- **`Hotkeys/Handlers/DeleteSelectionHotkeyHandler.cs`** - Trong M9 → hotkey: Delete → xóa selection phe local (chậm / có hiệu ứng Die).
- **`Hotkeys/Handlers/DeleteSelectionImmediateHotkeyHandler.cs`** - Trong M9 → hotkey: Shift+Delete → xóa ngay selection phe local.
- **`Hotkeys/Handlers/HotkeyDebugLogHandler.cs`** - Trong M9 → hotkey: Debug: log mọi hotkey được kích hoạt (tắt trên build release).
- **`Hotkeys/Handlers/StopUnitsHotkeyHandler.cs`** - Trong M9 → hotkey: H → gọi IHotkeyStopUnitsTarget. Mẫu handler lệnh unit.
- **`Hotkeys/Handlers/UnitTypeSelectHotkeyHandlerDelegate.cs`** - Trong M9 → hotkey: SRP: Handler không-MonoBehaviour — một cặp HotkeyId + prefab unit.
- **`Hotkeys/HotkeyBindingCatalogSO.cs`** - Trong M9 → hotkey: Dữ liệu cấu hình mapping phím → HotkeyId. Tạo asset: Create > RTS > Hotkeys > Binding Catalog.
- **`Hotkeys/HotkeyBindingEntry.cs`** - Trong M9 → hotkey: Một hành động có thể gán nhiều tổ hợp phím thay thế (OR).
- **`Hotkeys/HotkeyBindingPreferences.cs`** - Trong M9 → hotkey: SRP: Lưu/đọc override hotkey trong PlayerPrefs để runtime và UI dùng chung.
- **`Hotkeys/HotkeyChord.cs`** - Trong M9 → hotkey: Một tổ hợp phím (modifier + phím chính). So khớp theo Input System Key.
- **`Hotkeys/HotkeyComponentUtility.cs`** - Trong M9 → hotkey: Tìm interface trên cùng GameObject (Unity không hỗ trợ GetComponent trực tiếp với interface).
- **`Hotkeys/HotkeyContext.cs`** - Trong M9 → hotkey: Type HotkeyContext.
- **`Hotkeys/HotkeyDefaults.cs`** - Trong M9 → hotkey: Mapping mặc định UTS (QWERTY). Dùng khi chưa gán HotkeyBindingCatalogSO.
- **`Hotkeys/HotkeyHandlerBase.cs`** - Trong M9 → hotkey: Base MonoBehaviour cho handler: kéo vào HotkeySystem hoặc để con của cùng GameObject.
- **`Hotkeys/HotkeyId.cs`** - Trong M9 → hotkey: Định danh phím tắt. Thêm giá trị mới ở cuối enum khi mở rộng tính năng. Tham chiếu mô tả người chơi: Resources/UI/hotkeys_uts_vi.json
- **`Hotkeys/HotkeyService.cs`** - Trong M9 → hotkey: SRP: Quét binding, gọi handler đã đăng ký. Không phụ thuộc MonoBehaviour.
- **`Hotkeys/HotkeySystem.cs`** - Trong M9 → hotkey: Entry point: gắn vào scene, tick HotkeyService mỗi frame.
- **`Hotkeys/IHotkeyGate.cs`** - Trong M9 → hotkey: Chặn hotkey khi đang nhập chat/UI (ISP: gate nhỏ, một nhiệm vụ).
- **`Hotkeys/IHotkeyHandler.cs`** - Trong M9 → hotkey: Một handler xử lý đúng một HotkeyId. Thêm class mới implement interface này để mở rộng.
- **`Hotkeys/IHotkeyInputSource.cs`** - Trong M9 → hotkey: Đọc trạng thái bàn phím (DIP: HotkeyService không phụ thuộc Unity Input trực tiếp).
- **`Hotkeys/LocalHumanHotkeyGate.cs`** - Trong M9 → hotkey: Chặn hotkey khi MP chưa gán LocalOwner (P2 vào game trễ, lobby chưa sync…). Offline: không chặn (LocalHumanOwnerService tự bootstrap P1).
- **`Hotkeys/Targets/IHotkeyActionBarTarget.cs`** - Trong M9 → hotkey: Bridge phím 1–9 → lệnh slot trên thanh action của selection hiện tại.
- **`Hotkeys/Targets/IHotkeyCameraTarget.cs`** - Trong M9 → hotkey: Bridge hotkey Home / F → camera gameplay.
- **`Hotkeys/Targets/IHotkeyCancelTarget.cs`** - Trong M9 → hotkey: Abstraction cho hủy chọn / hủy đặt công trình (Esc). PlayerInput có thể implement sau.
- **`Hotkeys/Targets/IHotkeyDeleteSelectionTarget.cs`** - Trong M9 → hotkey: Bridge hotkey Delete / Shift+Delete → xóa selection phe local.
- **`Hotkeys/Targets/IHotkeyStopUnitsTarget.cs`** - Trong M9 → hotkey: Abstraction cho lệnh Stop (H). Unit selection controller implement sau.
- **`Hotkeys/Targets/IHotkeyUnitTypeSelectTarget.cs`** - Trong M9 → hotkey: Bridge hotkey Q/W/E/R → chọn unit theo prefab archetype trên màn hình.
- **`Hotkeys/UiFocusHotkeyGate.cs`** - Trong M9 → hotkey: Chặn hotkey khi người chơi đang focus ô nhập liệu (chat, search…).
- **`Hotkeys/UnitTypeHotkeySetup.cs`** - Trong M9 → hotkey: SRP: Gắn prefab archetype → HotkeyId và đăng ký delegate lên HotkeyService.
- **`Hotkeys/UnityHotkeyInputSource.cs`** - Trong M9 → hotkey: Đọc phím qua Input System. Keyboard được cache một lần mỗi frame trong Tick.
```

### Events / Player liên quan

```
- **`Events/HotkeyTriggeredEvent.cs`** - Trong M9 → hotkey: Payload Bus<T> theo Owner.
- **`Player/PlayerInputHotkeyIntegration.cs`** - Trong M9 → hotkey: SRP: Gắn hotkey stack lên PlayerInput và bridge Esc/H vào gameplay. Tương thích MP: event bus theo LocalOwner; Stop relay qua PlayerInputNetworkBridge.
```

---

## M10 — Lệnh giọng nói (Speech Recognition)

**Mục tiêu:** STT (Whisper) → fuzzy match dataset → thực thi lệnh game.

**Phụ thuộc:** M0, M2 (`PlayerInput.Voice.cs`, `VoiceCommandGameplayExecutor`).

**Dữ liệu ngoài Scripts:**

```
Assets/Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json
Assets/Resources/VoiceCommands/rts_voice_commands_standard_vi.json
```

### Thứ tự đọc gợi ý

1. ★ `SpeechRecognition/Core/VoiceCommandRouter.cs`
2. ★ `SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs`
3. ★ `SpeechRecognition/VoiceCommandGameplayExecutor.cs`
4. `SpeechRecognition/VoiceCommandPushToTalkInput.cs`
5. `SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs`
6. `SpeechRecognition/Core/VoiceCommandIdActionRules.cs`, `VoiceCommandProfile.cs`

### Danh sách file — `SpeechRecognition/` (27)

```
- **`SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs`** - Trong M10 → voice command: So khớp chuỗi STT với primary/alias; câu tương tự được ánh xạ về PrimaryPhrase mẫu (SRP: fuzzy map).
- **`SpeechRecognition/Core/ISpeechRecognitionBackend.cs`** - Trong M10 → voice command: Abstraction for streaming speech-to-text. High-level game code depends on this instead of Vosk types (DIP/ISP).
- **`SpeechRecognition/Core/IVoiceCommandResolver.cs`** - Trong M10 → voice command: Ánh xạ chuỗi STT sang id lệnh game (DIP: gameplay không phụ thuộc Vosk).
- **`SpeechRecognition/Core/RecognizedSpeechPhraseMapper.cs`** - Trong M10 → voice command: Tìm cụm mẫu gần nhất và trả PrimaryPhrase chuẩn (SRP: ánh xạ câu tương tự → mẫu dataset).
- **`SpeechRecognition/Core/RecognizedSpeechPhraseNormalizer.cs`** - Trong M10 → voice command: Đưa text STT về cùng dạng với mẫu câu trong dataset (không dấu, không dấu câu, chữ thường).
- **`SpeechRecognition/Core/SpeechRecognitionBackendBehaviour.cs`** - Trong M10 → voice command: Unity-facing hook for backends. Lets the microphone driver reference a single inspector field (OCP: swap backends).
- **`SpeechRecognition/Core/SpeechRecognitionDebugLogger.cs`** - Trong M10 → voice command: Ghi partial/final ra Console để kiểm tra nhanh (SRP: chỉ log).
- **`SpeechRecognition/Core/StringSimilarity.cs`** - Trong M10 → voice command: Khoảng cách Levenshtein và độ tương đồng chuẩn hóa (SRP: chỉ so khớp chuỗi).
- **`SpeechRecognition/Core/VietnameseTextNormalizer.cs`** - Trong M10 → voice command: Chuẩn hóa chuỗi tiếng Việt cho STT / fuzzy / grammar Vosk (SRP: chỉ xử lý text).
- **`SpeechRecognition/Core/VoiceCommandDatasetFile.cs`** - Trong M10 → voice command: Root JSON cho tập lệnh thoại (OCP: mở rộng bằng file, không sửa code gameplay). JsonUtility: tên field khớp với JSON (schemaVersion, minSimilarity, commands).
- **`SpeechRecognition/Core/VoiceCommandDocsDatasetImporter.cs`** - Trong M10 → voice command: Chuyển file docs/voice-command-dataset.vi.json (schema intents) sang VoiceCommandDatasetFile (commands).
- **`SpeechRecognition/Core/VoiceCommandIdActionRules.cs`** - Trong M10 → voice command: Quy tắc ánh xạ CommandId (JSON voice) → kiểu hành động / predicate tìm BaseCommand trên selection.
- **`SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs`** - Trong M10 → voice command: Nối transcript one-shot sang command id theo dataset JSON trong Resources.
- **`SpeechRecognition/Core/VoiceCommandProfile.cs`** - Trong M10 → voice command: Một lệnh thoại: id game + câu chính + alias (OCP: mở rộng bằng asset, không sửa code).
- **`SpeechRecognition/Core/VoiceCommandRouter.cs`** - Trong M10 → voice command: Nối STT → resolver → sự kiện Unity cho gameplay (SRP: không chứa logic Vosk).
- **`SpeechRecognition/Core/WhisperSpeechDefaults.cs`** - Trong M10 → voice command: Đường dẫn model Whisper trong StreamingAssets (SRP: hằng số cấu hình).
- **`SpeechRecognition/Core/WhisperStreamingTuning.cs`** - Trong M10 → voice command: Gợi ý thông số streaming Whisper (SRP: chỉ tuning, không chạy inference).
- **`SpeechRecognition/Core/WhisperVoicePromptBuilder.cs`** - Trong M10 → voice command: Sinh initial prompt cho Whisper từ tập lệnh RTS (SRP: chỉ build prompt).
- **`SpeechRecognition/Editor/SpeechRecognitionSandboxSceneBuilder.cs`** - Trong M10 → voice command: Tạo scene test STT (Whisper) — không gắn map lệnh / gameplay voice.
- **`SpeechRecognition/SpeechRecognitionSandboxFeedback.cs`** - Trong M10 → voice command: Hiển thị STT và kết quả fuzzy trên Canvas trong scene sandbox (SRP: chỉ UI phản hồi).
- **`SpeechRecognition/VoiceCommandGameplayExecutor.cs`** - Trong M10 → voice command: Nối CommandId (sau fuzzy voice) → thực thi gameplay qua PlayerInput.
- **`SpeechRecognition/VoiceCommandOneShotCapture.cs`** - Trong M10 → voice command: Thu một câu STT (Whisper) → đăng transcript lên chat — không ánh xạ lệnh game.
- **`SpeechRecognition/VoiceCommandPushToTalkInput.cs`** - Trong M10 → voice command: Phím giữ để thu thoại (SRP: chỉ input qua Input System).
- **`SpeechRecognition/VoiceCommandRuntimeDiagnostics.cs`** - Trong M10 → voice command: Log một lần khi Play để kiểm tra voice đã gắn trong scene chính (SRP: chẩn đoán).
- **`SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs`** - Trong M10 → voice command: Adapter Whisper streaming (theo mẫu StreamingSampleMic) → ISpeechRecognitionBackend (DIP).
- **`SpeechRecognition/Whisper/WhisperSttOnlyDriver.cs`** - Trong M10 → voice command: Chỉ nhận diện giọng nói (STT) — copy luồng StreamingSampleMic từ whisper.unity (SRP: không map lệnh game).
- **`SpeechRecognition/Whisper/WhisperSttRecordControls.cs`** - Trong M10 → voice command: Một nút bật/tắt thu âm STT (SRP: chỉ điều khiển UI, không xử lý Whisper).
```

### File Player liên quan

```
- **`Player/PlayerInput.Voice.cs`** - Trong M10 → voice command: SRP: Thực thi CommandId từ voice → selection / arm command / stop / gather.
```

---

## M11 — UI (in-game & menu)

**Mục tiêu:** Action bar, panel chọn unit/nhà, pregame, event log, overlay settings.

**Phụ thuộc:** M0, Events (`Bus<T>`), M2–M4.

### Thứ tự đọc gợi ý

1. ★ `UI/RuntimeUI.cs`
2. ★ `UI/Containers/ActionsUI.cs`, `ActionBarCommandExecution.cs`
3. `UI/Containers/SingleUnitSelectedUI.cs`, `MultiUnitSelectionUI.cs`
4. `UI/Pregame/MainMenuUIController.cs`, `PregameSetupUIController.cs`
5. `UI/GameEventLog/GameEventLogUI.cs`, `PlayerGameEventLogListener.cs`

### Danh sách file — `UI/` (50)

```
- **`UI/ActionBarCommandExecution.cs`** - Trong M11 → UI: SRP: Kiểm tra có thể kích hoạt lệnh từ thanh action / phím số hay không.
- **`UI/ActionBarCommandResolver.cs`** - Trong M11 → UI: SRP: Lấy lệnh theo slot (0–8) cho selection hiện tại — cùng quy tắc với Containers.ActionsUI.
- **`UI/Components/FreeDraggablePanel.cs`** - Trong M11 → UI: Kéo panel UI tự do trong canvas cha (giống chat log). Gắn lên vùng kéo (title bar / header); gán Panel = khung cần di chuyển.
- **`UI/Components/HoverRevealPanel.cs`** - Trong M11 → UI: Hover vào object này → hiện panel; rời chuột → tắt panel. Gắn cùng GameObject có Image/Button (raycastTarget bật).
- **`UI/Components/MultiUnitTypeSlotUI.cs`** - Trong M11 → UI: Một ô loại unit trong panel chọn nhiều unit (icon + số lượng).
- **`UI/Components/ProgressBar.cs`** - Trong M11 → UI: Type ProgressBar.
- **`UI/Components/Tooltip.cs`** - Trong M11 → UI: Type Tooltip.
- **`UI/Components/UIActionButton.cs`** - Trong M11 → UI: Type UIActionButton.
- **`UI/Components/UIBuildQueueButton.cs`** - Trong M11 → UI: Type UIBuildQueueButton.
- **`UI/Components/UiExclusiveSelectGroup.cs`** - Trong M11 → UI: Nhóm chọn một trong nhiều option (map list, độ khó AI…). Đặt trên parent; tự thu thập UiExclusiveSelectOption ở con nếu chưa gán.
- **`UI/Components/UiExclusiveSelectOption.cs`** - Trong M11 → UI: Một lựa chọn trong nhóm exclusive — Button + ảnh chọn / không chọn + text (text nằm ngoài Button cũng được).
- **`UI/Components/UiPanelActivation.cs`** - Trong M11 → UI: SRP: Bật panel/dialog sau khi Awake/OnEnable lần đầu của hierarchy con đã chạy xong.
- **`UI/Components/UIUnitButton.cs`** - Trong M11 → UI: Type UIUnitButton.
- **`UI/Components/UnitStatSlotUI.cs`** - Trong M11 → UI: Một ô stat (prefab Armor Damage Icon): level upgrade, giá trị, icon, tooltip.
- **`UI/Components/UnitWorldHealthBar.cs`** - Trong M11 → UI: Thanh máu world-space: fill theo HP và đổi sprite theo Owner (Player1 / Player2).
- **`UI/Containers/ActionsUI.cs`** - Trong M11 → UI: Thanh action bar — render nút lệnh theo selection và ActiveCommand.
- **`UI/Containers/BuildingBuildingUI.cs`** - Trong M11 → UI: Type BuildingBuildingUI.
- **`UI/Containers/BuildingSelectedUI.cs`** - Trong M11 → UI: Type BuildingSelectedUI.
- **`UI/Containers/BuildingUnderConstructionUI.cs`** - Trong M11 → UI: Type BuildingUnderConstructionUI.
- **`UI/Containers/MultiUnitSelectionUI.cs`** - Trong M11 → UI: Panel giữa màn hình khi chọn từ 2 unit trở lên: icon theo loại + số lượng.
- **`UI/Containers/SingleUnitSelectedUI.cs`** - Trong M11 → UI: Type SingleUnitSelectedUI.
- **`UI/Containers/UnitIconUI.cs`** - Trong M11 → UI: Type UnitIconUI.
- **`UI/Containers/UnitStatsPanelUI.cs`** - Trong M11 → UI: Panel các ô stat khi chọn một unit (Damage, tốc chạy, tốc đánh, tầm…).
- **`UI/Containers/UnitTransportUI.cs`** - Trong M11 → UI: Type UnitTransportUI.
- **`UI/GameEventLog/GameEventLog.cs`** - Trong M11 → UI: In-memory event log buffer; UI and other systems subscribe to LineAdded.
- **`UI/GameEventLog/GameEventLogCategory.cs`** - Trong M11 → UI: Enum phân loại dòng log (economy, combat, system) cho format/màu.
- **`UI/GameEventLog/GameEventLogLine.cs`** - Trong M11 → UI: Type GameEventLogLine.
- **`UI/GameEventLog/GameEventLogLineFormatter.cs`** - Trong M11 → UI: Builds display text for a single log line (TMP rich text).
- **`UI/GameEventLog/GameEventLogLineView.cs`** - Trong M11 → UI: One chat row prefab; height driven by LayoutElement preferred height for VerticalLayoutGroup.
- **`UI/GameEventLog/GameEventLogMessageFormatter.cs`** - Trong M11 → UI: Converts game bus events into player-facing log strings.
- **`UI/GameEventLog/GameEventLogScrollController.cs`** - Trong M11 → UI: Tutorial-style chat scroll: when Content children change, scroll to the newest line at the bottom.
- **`UI/GameEventLog/GameEventLogUI.cs`** - Trong M11 → UI: Game event log: append text + ScrollRect/Viewport/Mask (không tràn viền, cuộn được).
- **`UI/GameEventLog/PlayerGameEventLogListener.cs`** - Trong M11 → UI: Subscribes to Bus{T} events for Player1 and posts messages to GameEventLog.
- **`UI/InGame/InGameOverlayMenuController.cs`** - Trong M11 → UI: SRP: HUD in-game — nút tốc độ / menu, panel con (speed, menu, pause, settings, đầu hàng).
- **`UI/InGame/InGameSettingsPanelOpener.cs`** - Trong M11 → UI: Type InGameSettingsPanelOpener.
- **`UI/IUIElement.cs`** - Trong M11 → UI: Type IUIElement.
- **`UI/OwnerHealthBarStyleSO.cs`** - Trong M11 → UI: ScriptableObject cau hinh du lieu.
- **`UI/Pregame/ExitConfirmDialog.cs`** - Trong M11 → UI: SRP: Dialog xác nhận 2 nút (OK / Hủy) — thoát app hoặc hành động tùy chỉnh (đầu hàng…).
- **`UI/Pregame/GameManualScrollViewBinder.cs`** - Trong M11 → UI: Start: đọc JSON và gán rich text (in đậm theo từng dòng) vào TextMeshProUGUI.
- **`UI/Pregame/HotkeySettingItemView.cs`** - Trong M11 → UI: SRP: View item một dòng hotkey trong scroll view.
- **`UI/Pregame/HotkeySettingsScrollViewBinder.cs`** - Trong M11 → UI: SRP: Đổ danh sách hotkey vào ScrollView Content bằng prefab HotKeySetting.
- **`UI/Pregame/MainMenuSettingsDialogController.cs`** - Trong M11 → UI: SRP: Quản lý dialog cài đặt MainMenu (tab Audio/Hotkey, tên người chơi, Save/Reset theo cơ chế staging).
- **`UI/Pregame/MainMenuUIController.cs`** - Trong M11 → UI: SRP: Điều khiển menu chính MainMenu — nút Play, dialog, thoát game. Gắn lên Canvas hoặc Panel; có thể auto-bind theo tên GameObject nếu để trống SerializeField.
- **`UI/Pregame/ParallaxMenuBackground.cs`** - Trong M11 → UI: Trượt Image trái/phải trong khoảng ±Move Distance. Lớp 1 đứng yên, lớp 2 = X% tốc độ, lớp 3 = 100%.
- **`UI/Pregame/PregameMapEntry.cs`** - Trong M11 → UI: Dữ liệu một map trên màn setup (tên hiển thị, ảnh minh họa, scene gameplay).
- **`UI/Pregame/PregameMapSelectItemView.cs`** - Trong M11 → UI: SRP: Một nút map trong scrollview — nhãn, trạng thái chọn (Img Select / Unselect).
- **`UI/Pregame/PregameMapSelectScrollBinder.cs`** - Trong M11 → UI: SRP: Nguồn cấu hình map duy nhất trên SSScene — catalog, spawn nút, preview, scene đã chọn. Chỉ chỉnh mảng Maps + template/Content trên component này (không khai báo lại trên PregameSetupUIController).
- **`UI/Pregame/PregameMpLobbyCoordinator.cs`** - Trong M11 → UI: SRP: Điều phối quyền lobby MP trên SSScene — client chỉ Ready/Back, host chọn map và Start.
- **`UI/Pregame/PregameSetupUIController.cs`** - Trong M11 → UI: SRP: Màn setup SSScene — điều phối chọn map, độ khó AI, bắt đầu trận / lobby.
- **`UI/RuntimeUI.cs`** - Trong M11 → UI: SRP: Panel lệnh / selection UI — subscribe Bus theo một Owner (P1 hoặc P2).
```

---

## M12 — Audio

**Mục tiêu:** Nhạc menu/gameplay, SFX, volume, listener theo gate.

**Phụ thuộc:** M1 (`GameplayStartupGate`).

### Thứ tự đọc gợi ý

1. ★ `Audio/AudioBootstrap.cs`, `AudioAccess.cs`
2. ★ `Audio/IAudioPlaybackService.cs`, `AudioPlaybackService.cs`
3. `Audio/MenuAudioController.cs`, `PregameAudioTransition.cs`
4. `Audio/PlayerAudioListener.cs`

### Danh sách file — `Audio/` (18)

```
- **`Audio/AttackAudioUtility.cs`** - Trong M12 → audio: SRP: Chọn cue tấn công theo AttackConfigSO.
- **`Audio/AudioAccess.cs`** - Trong M12 → audio: SRP: Static accessor cho playback service sau khi AudioBootstrap khởi tạo.
- **`Audio/AudioBootstrap.cs`** - Trong M12 → audio: SRP: Khởi tạo AudioPlaybackService và giữ reference DontDestroyOnLoad.
- **`Audio/AudioChannel.cs`** - Trong M12 → audio: Enum kênh mixer: Sfx, Ui, Voice, Music.
- **`Audio/AudioClipCatalogSO.cs`** - Trong M12 → audio: ScriptableObject cau hinh du lieu.
- **`Audio/AudioCueId.cs`** - Trong M12 → audio: Type AudioCueId.
- **`Audio/AudioMixerParameterNames.cs`** - Trong M12 → audio: Exposed parameter names on RTS Audio Mixer — phải khớp tên trong Unity Audio Mixer.
- **`Audio/AudioPlayback3DSettings.cs`** - Trong M12 → audio: Type AudioPlayback3DSettings.
- **`Audio/AudioPlaybackService.cs`** - Trong M12 → audio: SRP: Phát clip 2D từ catalog, pool AudioSource, nhạc nền loop riêng.
- **`Audio/AudioSettingsVolumePanelBinder.cs`** - Trong M12 → audio: SRP: Gắn mọi Slider trong panel Settings theo tên hàng (Master, Music, Sfx, UI, Voice). Đặt trên root Dialog Setting hoặc Panel Setting.
- **`Audio/AudioVolumeChannel.cs`** - Trong M12 → audio: Type AudioVolumeChannel.
- **`Audio/AudioVolumeController.cs`** - Trong M12 → audio: SRP: Lưu/load volume (PlayerPrefs), áp dụng lên AudioMixer và/hoặc AudioPlaybackService.
- **`Audio/AudioVolumeSliderBinder.cs`** - Trong M12 → audio: SRP: Gắn Slider UI → AudioVolumeController cho một kênh volume.
- **`Audio/IAudioPlaybackService.cs`** - Trong M12 → audio: Type IAudioPlaybackService.
- **`Audio/MenuAudioController.cs`** - Trong M12 → audio: SRP: Âm thanh menu — nhạc nền MenuMusic + click UiSelect trên mọi Button con. MainMenu: startMenuMusic=true. SSScene: startMenuMusic=false (giữ nhạc từ MainMenu qua AudioBootstrap DontDestroyOnLoad).
- **`Audio/PlayerAudioListener.cs`** - Trong M12 → audio: SRP: Map Bus{T} gameplay events → AudioCueId cho local player.
- **`Audio/PregameAudioTransition.cs`** - Trong M12 → audio: SRP: Chuyển giao audio giữa menu/setup và gameplay.
- **`Audio/SupplyGatherAudioUtility.cs`** - Trong M12 → audio: SRP: Map SupplySO → cue gather (stone / wood / food).
```

---

## M13 — AI đối thủ

**Mục tiêu:** Tick AI (economy → base → military), dispatch lệnh không qua `PlayerInput`.

**Phụ thuộc:** M0, M3–M5, Commands.

**Tài liệu thêm:** `docs/HUONG_DAN_CHAY_AI.md`, `docs/AI_SANDBOX_CHECKLIST.md`

### Thứ tự đọc gợi ý

1. ★ `AI/Core/AIController.cs`
2. ★ `AI/Core/AICommandDispatcher.cs`, `AICommandIntent.cs`
3. ★ `AI/State/AIWorldState.cs`, `AIUnitRegistry.cs`
4. ★ `AI/Managers/AIEconomyManager.cs` → `AIBaseManager.cs` → `AIMilitaryManager.cs`
5. `AI/Strategy/AIInfluenceMap.cs`, `AIPriorityQueue.cs`
6. `AI/Sandbox/AISandboxWinLoseChecklist.cs`

### Danh sách file — `AI/Config/` (4)

```
- **`AI/Config/AIDifficultyLevel.cs`** - Trong M13 → AI đối thủ: Ba mức độ khó ship mặc định (Dễ / Trung bình / Khó).
- **`AI/Config/AIDifficultyRuntimeOverlay.cs`** - Trong M13 → AI đối thủ: Type AIDifficultyRuntimeOverlay.
- **`AI/Config/AIDifficultySO.cs`** - Trong M13 → AI đối thủ: SRP: Tham số độ khó AI — một asset cho mỗi chế độ (Dễ / Trung bình / Khó). Planner đọc overlay từ SO này; không hardcode ngưỡng trong manager.
- **`AI/Config/AIGameSessionConfigSO.cs`** - Trong M13 → AI đối thủ: SRP: Chọn độ khó mặc định khi vào trận (menu / GameSetup).
```

### Danh sách file — `AI/Core/` (11)

```
- **`AI/Core/AICommandDispatcher.cs`** - Trong M13 → AI đối thủ: Thực thi lệnh gameplay qua BaseCommand — mirror GameDevTV.RTS.Player.PlayerInput, không UI/EventBus lệnh.
- **`AI/Core/AICommandIntent.cs`** - Trong M13 → AI đối thủ: Định danh manager cho logging và ưu tiên intent (M2+).
- **`AI/Core/AIController.cs`** - Trong M13 → AI đối thủ: Điều phối tick AI: registry → world state snapshot → (M2+) managers → dispatcher. Không gắn vào GameDevTV.RTS.Player.PlayerInput; không raise CommandSelectedEvent.
- **`AI/Core/AIFactionSightQuery.cs`** - Trong M13 → AI đối thủ: SRP: Tầm nhìn planner cho phe AI — union vòng SightRadius từ unit/building cùng owner (không dùng fog Player1).
- **`AI/Core/AIHitUtility.cs`** - Trong M13 → AI đối thủ: Tạo RaycastHit cho dispatcher — mirror click người chơi, không UI.
- **`AI/Core/AIOwnershipGuard.cs`** - Trong M13 → AI đối thủ: SRP: Xác nhận entity thuộc phe AI đang chạy planner/dispatcher.
- **`AI/Core/AIPlannerStatusFormatter.cs`** - Trong M13 → AI đối thủ: SRP: Một dòng trạng thái AI cho Game Event Log (thay thông báo gather từng lần).
- **`AI/Core/AIPlannerTickBudget.cs`** - Trong M13 → AI đối thủ: Type AIPlannerTickBudget.
- **`AI/Core/AISceneEntityCache.cs`** - Trong M13 → AI đối thủ: SRP: Cache entity toàn scene tối đa một lần mỗi frame — tránh FindObjectsByType lặp trong military/economy.
- **`AI/Core/AIWorkerCommandGuard.cs`** - Trong M13 → AI đối thủ: SRP: Chặn AI gán lệnh macro khi worker đang xây hoặc đang gather/return — tránh phá Behavior micro.
```

### Danh sách file — `AI/Managers/` (48)

```
- **`AI/Managers/AIBaseConfigResolver.cs`** - Trong M13 → AI đối thủ: SRP: Suy ra lệnh train/build/research và ngưỡng worker từ Civil Central + worker commands.
- **`AI/Managers/AIBaseManager.cs`** - Trong M13 → AI đối thủ: SRP: Train worker tại Civil Central; queue Store → Corral → Forge → Barrack → Tower; research Forge.
- **`AI/Managers/AIBasePriority.cs`** - Trong M13 → AI đối thủ: Priority bands — infra build luôn trên economy gather (400).
- **`AI/Managers/AIBuildCommandCatalog.cs`** - Trong M13 → AI đối thủ: SRP: Cache toàn bộ BuildBuildingCommand trong project (một lần) — fallback khi worker/Inspector chưa gán SO.
- **`AI/Managers/AIBuildingPlacementUtility.cs`** - Trong M13 → AI đối thủ: SRP: Tìm điểm đặt nhà cho AI — quét từ anchor, hợp lệ chỉ khi BuildBuildingCommand Restrictions pass.
- **`AI/Managers/AIBuildUnitCommandCatalog.cs`** - Trong M13 → AI đối thủ: SRP: Cache BuildUnitCommand trong project — fallback khi Barrack/Inspector chưa gán SO.
- **`AI/Managers/AIConstructionAssignment.cs`** - Trong M13 → AI đối thủ: Type AIConstructionAssignment.
- **`AI/Managers/AIEconomyConfigResolver.cs`** - Trong M13 → AI đối thủ: SRP: Suy ra thông số economy từ map + registry — không cần kéo asset/distance tay trên Inspector.
- **`AI/Managers/AIEconomyCorralPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Thiếu food → xây thêm Corral (passive food); không thay thế gather mỏ food.
- **`AI/Managers/AIEconomyFoodGatherUtility.cs`** - Trong M13 → AI đối thủ: SRP: Nhận diện mỏ food còn khai thác và trạng thái thiếu food trên kho.
- **`AI/Managers/AIEconomyGatherSupplyIndex.cs`** - Trong M13 → AI đối thủ: SRP: Lập danh sách mỏ gather visible hợp lệ theo loại — một lần mỗi tick economy (không lặp theo worker).
- **`AI/Managers/AIEconomyGatherTerritoryGuard.cs`** - Trong M13 → AI đối thủ: SRP: Chặn AI economy chọn mỏ/supply trong vùng nhà human (PvAI M5).
- **`AI/Managers/AIEconomyManager.cs`** - Trong M13 → AI đối thủ: SRP: Worker gather 40/40/20 hoặc 60% thiếu; hết mỏ food → 70/30 đá-gỗ; thiếu food → thêm Corral; Store xa CC. Mỗi worker: GatherCommand → BT Gather Sub Graph tự loop + tự return khi đầy (Petra: không spam ReturnSupplies). Chỉ mỏ GatherableSupply.IsVisible (fog gameplay chung với người chơi); khóa gather trên Worker.ShouldIssueGatherTo.
- **`AI/Managers/AIEconomyPriority.cs`** - Trong M13 → AI đối thủ: Priority bands — gather thấp hơn base infra build (760+).
- **`AI/Managers/AIEconomySupplyKindClassifier.cs`** - Trong M13 → AI đối thủ: SRP: Phân loại SupplySO thành đá / gỗ / food cho economy AI.
- **`AI/Managers/AIEconomyWildCorpseFoodUtility.cs`** - Trong M13 → AI đối thủ: SRP: Phân biệt mỏ food tĩnh (berry, farm) với xác thú sau săn — xác không được coi là "mỏ food" cho slot 7:3 đá-gỗ.
- **`AI/Managers/AIEconomyWildFoodPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Thiếu food và không có mỏ food visible — worker (hoặc lính rảnh) đi săn WildAnimal.
- **`AI/Managers/AIForgeResearchRoundUtility.cs`** - Trong M13 → AI đối thủ: SRP: Forge operational + gom lệnh research theo tier (dùng bởi AIForgeResearchTierPlanner).
- **`AI/Managers/AIForgeResearchTierPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Xen kẽ research theo tier (Damage 1, Health 1, …) với phase economy (build/gather).
- **`AI/Managers/AIInfraBuildOrderTracker.cs`** - Trong M13 → AI đối thủ: SRP: Nhớ loại nhà AI đã enqueue/dispatch build — tránh giao trùng trước khi prefab vào registry.
- **`AI/Managers/AIInfraBuildUtility.cs`** - Trong M13 → AI đối thủ: SRP: Thứ tự xây nhà base — Store → Corral → Forge → Barrack → Tower.
- **`AI/Managers/AIMilitaryArmyAssemblyTracker.cs`** - Trong M13 → AI đối thủ: Type AIMilitaryArmyAssemblyTracker.
- **`AI/Managers/AIMilitaryArmySquadExecutor.cs`** - Trong M13 → AI đối thủ: SRP: Một đội quân — Attack cả nhóm lên mục tiêu chung, rồi formation Move (không một điểm chồng).
- **`AI/Managers/AIMilitaryConfigResolver.cs`** - Trong M13 → AI đối thủ: SRP: Resolve tham số quân sự (tỷ lệ dân/quân, ngưỡng tấn công, lệnh train) cho tick hiện tại.
- **`AI/Managers/AIMilitaryDefenseRingBuildCoordinator.cs`** - Trong M13 → AI đối thủ: SRP: Đặt tháp trên vòng mỗi tick; nhịp 5 phút chỉ mở khóa thêm vòng bán kính.
- **`AI/Managers/AIMilitaryDefenseRingCombatUtility.cs`** - Trong M13 → AI đối thủ: SRP: Combat trong vùng leash — đánh địch thấy ngay; Stop khi unit ra khỏi vùng.
- **`AI/Managers/AIMilitaryDefenseRingEconomyRecovery.cs`** - Trong M13 → AI đối thủ: SRP: Thiếu tài nguyên khi spam tháp nhiều lần → yêu cầu thêm worker / Corral.
- **`AI/Managers/AIMilitaryDefenseRingPlacementUtility.cs`** - Trong M13 → AI đối thủ: SRP: Điểm đặt Barrack/Tháp trên vòng cố định quanh CC.
- **`AI/Managers/AIMilitaryDefenseRingPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Vòng tháp 100m +100m/vòng; nhịp 5 phút chỉ mở khóa thêm vòng (bán kính), không gate đặt tháp.
- **`AI/Managers/AIMilitaryDefenseRingTowerArmyGate.cs`** - Trong M13 → AI đối thủ: SRP: Ngưỡng spawn quân trước mỗi tháp vòng — tổng đã spawn (kể cả chết), bậc × (số tháp + 1).
- **`AI/Managers/AIMilitaryEnemyTracker.cs`** - Trong M13 → AI đối thủ: SRP: Vị trí Civil Central địch đã từng thấy (fair fog) — mục tiêu attack wave.
- **`AI/Managers/AIMilitaryExpansionPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Phase XP đầu (hạ tầng quân) và phase mở rộng (train + tấn công).
- **`AI/Managers/AIMilitaryHostileScanner.cs`** - Trong M13 → AI đối thủ: SRP: Quét địch / Civil Central địch trong scene (một lần mỗi tick military) — không cache registry owner khác.
- **`AI/Managers/AIMilitaryLoosePatrolPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Tuần tra lẻ quanh CC trong vòng bán kính — không formation, mỗi lính một điểm Move riêng.
- **`AI/Managers/AIMilitaryManager.cs`** - Trong M13 → AI đối thủ: SRP: Train Barrack, phòng thủ Civil Central, hàng Defense Tower, attack wave tới CC địch.
- **`AI/Managers/AIMilitaryMapExplorationUtility.cs`** - Trong M13 → AI đối thủ: SRP: Đánh giá vùng patrol đã được khám phá đủ chưa (bán kính patrol + fog explored).
- **`AI/Managers/AIMilitaryOperationalLeash.cs`** - Trong M13 → AI đối thủ: SRP: Giới hạn hoạt động quân trong đĩa quanh CC (vòng tháp ngoài + leash).
- **`AI/Managers/AIMilitaryPatrolUtility.cs`** - Trong M13 → AI đối thủ: SRP: Điểm patrol scout — vòng tròn mở rộng dần quanh Civil Central của phe AI (không hướng thẳng CC địch).
- **`AI/Managers/AIMilitaryPostContactOffensePlanner.cs`** - Trong M13 → AI đối thủ: SRP: Sau khi scout thấy địch không còn tụ — đợi khám phá + xây (mặc định 2 phút) rồi tổng tấn công về CC địch.
- **`AI/Managers/AIMilitaryPriority.cs`** - Trong M13 → AI đối thủ: Priority bands — phòng thủ CC trên train Barrack và tấn công.
- **`AI/Managers/AIMilitaryRallyCombatPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Phase Attacking — gán Attack lên địch visible; Move formation chỉ là fallback.
- **`AI/Managers/AIMilitaryRallyPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Phát hiện nhà chính / đối thủ RTS (không animal) và chọn điểm rally tấn công formation.
- **`AI/Managers/AIMilitaryRallySessionTracker.cs`** - Trong M13 → AI đối thủ: SRP: Phiên rally hai pha — lùi tập hợp rồi mới tấn công (theo Owner AI).
- **`AI/Managers/AIMilitaryStagingPlacementUtility.cs`** - Trong M13 → AI đối thủ: SRP: Điều chỉnh điểm formation/staging — tránh chồng lên công trường đang xây.
- **`AI/Managers/AIMilitaryUnitMixPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Chọn train Barrack theo <b>tỷ lệ %</b> trên tổng quân (active + queue) — deficit = targetShare − currentShare.
- **`AI/Managers/AIMilitaryUnitMixSlot.cs`** - Trong M13 → AI đối thủ: Một loại quân trong cơ cấu Barrack — lệnh Build Unit + trọng số spawn.
- **`AI/Managers/AIWorkerGatherRefreshPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Mỗi N tick AI — Stop rồi Move ngắn để worker thoát gather cũ; economy vẫn gán mỏ cho worker rảnh cùng tick.
- **`AI/Managers/AIWorkerGatherSlotPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Cân bằng 40% đá / 40% gỗ / 20% food; mất cân bằng 60% thiếu + 20% mỗi loại kia. Hết mỏ food visible → chỉ đá/gỗ theo tỷ lệ 7:3 (phần lớn tối thiểu 7 worker khi đủ quy mô).
```

### Danh sách file — `AI/State/` (2)

```
- **`AI/State/AIUnitRegistry.cs`** - Trong M13 → AI đối thủ: Cache unit/building/supply từ EventBus theo Owner — không Find trong tick.
- **`AI/State/AIWorldState.cs`** - Trong M13 → AI đối thủ: Type AIWorldState.
```

### Danh sách file — `AI/Strategy/` (6)

```
- **`AI/Strategy/AIInfluenceMap.cs`** - Trong M13 → AI đối thủ: SRP: Lưới influence thô (XZ) — defense quanh Civil Central, threat địch, economic cụm mỏ xa. Dùng cho AIBaseManager / AIEconomyManager / AIMilitaryManager.
- **`AI/Strategy/AIInfluenceMapConfigResolver.cs`** - Trong M13 → AI đối thủ: SRP: Tự tính tỷ lệ influence (cell, bán kính, weight) từ footprint nhà và phạm vi đặt base.
- **`AI/Strategy/AIInfluenceMapTickContext.cs`** - Trong M13 → AI đối thủ: Type AIInfluenceMapTickContext.
- **`AI/Strategy/AIInfluenceMapTickPlanner.cs`** - Trong M13 → AI đối thủ: SRP: Rebuild AIInfluenceMap một lần mỗi tick từ AIWorldStateSnapshot.
- **`AI/Strategy/AIInfluencePlacementUtility.cs`** - Trong M13 → AI đối thủ: SRP: Chọn điểm đặt nhà — ưu tiên cell influence an toàn, fallback quét NavMesh + Restrictions + field 20m.
- **`AI/Strategy/AIPriorityQueue.cs`** - Trong M13 → AI đối thủ: Priority bands theo plan V2 (cao → thấp).
```

### Danh sách file — `AI/Sandbox/` (1)

```
- **`AI/Sandbox/AISandboxWinLoseChecklist.cs`** - Trong M13 → AI đối thủ: SRP: Theo dõi win/lose Civil Central trong scene sandbox — tick checklist cho QA. Gắn cùng GameObject có PlayerGameEventLogListener hoặc GameEventLog.
```

---

## M15 — Multiplayer (Netplay)

**Mục tiêu:** Mirror sync, spawn MP, relay lệnh client/server.

**Phụ thuộc:** M1, M2.

### Thứ tự đọc gợi ý

1. ★ `Netplay/RtsNetGameSceneBootstrap.cs`
2. ★ `Netplay/RtsMatchServerSpawnOrchestrator.cs`
3. `Netplay/RtsUtsClientCommandRelay.cs`, `MpLocalOwnerSceneSync.cs`
4. `Player/LocalHumanOwnerMirrorBridge.cs`

### Danh sách file — `Netplay/` (14)

```
- **`Netplay/MpFogRefreshThrottle.cs`** - Trong M15 → multiplayer: SRP: Giới hạn tần suất refresh fog/visibility MP — tránh lag do FindObjects + full pass lặp.
- **`Netplay/MpFogVisionSpawnRefresh.cs`** - Trong M15 → multiplayer: SRP: Sau khi unit/building MP replicate — refresh layer vision + fog plane texture cho local human.
- **`Netplay/MpLocalOwnerSceneSync.cs`** - Trong M15 → multiplayer: SRP: Sau load RtsNet_Game — gán LocalOwner từ RtsLobbyPlayer và bật presentation rig.
- **`Netplay/OwnerTeamMapping.cs`** - Trong M15 → multiplayer: SRP: Ánh xạ slot/team Mirror sang Owner human của UTS.
- **`Netplay/PlayerInputNetworkBridge.cs`** - Trong M15 → multiplayer: DIP: PlayerInput (game) gọi relay lệnh MP qua handler đăng ký (không phụ thuộc Mirror trực tiếp).
- **`Netplay/RtsMatchServerSpawnOrchestrator.cs`** - Trong M15 → multiplayer: SRP: Khi server vào scene trận — spawn Civil Central + worker UTS cho mỗi connection.
- **`Netplay/RtsMatchServerSpawnRunner.cs`** - Trong M15 → multiplayer: SRP: Retry spawn UTS sau khi Mirror chuyển scene — identity lobby đôi khi chưa sẵn sàng ngay OnServerSceneChanged.
- **`Netplay/RtsNetGameSceneBootstrap.cs`** - Trong M15 → multiplayer: SRP: Bootstrap RtsNet_Game — sync LocalOwner, presentation MP, tắt capsule input.
- **`Netplay/RtsUtsClientCommandRelay.cs`** - Trong M15 → multiplayer: Type RtsUtsClientCommandRelay.
- **`Netplay/RtsUtsCommandMirrorUtility.cs`** - Trong M15 → multiplayer: SRP: Tìm mỏ / target combat gần điểm click cho ClientRpc mirror (supply thường không có NetworkIdentity).
- **`Netplay/RtsUtsGameSceneSetup.cs`** - Trong M15 → multiplayer: SRP: Cấu hình prefab UTS và spawn điểm cho scene RtsNet_Game (thay capsule MVP khi đã wire).
- **`Netplay/RtsUtsNetworkEntity.cs`** - Trong M15 → multiplayer: Type RtsUtsNetworkEntity.
- **`Netplay/RtsUtsPlayerCommands.cs`** - Trong M15 → multiplayer: SRP: Commands Mirror cho unit/building UTS (game assembly, gắn cùng player prefab với RtsGameCommander).
- **`Netplay/RtsUtsServerSpawnHandler.cs`** - Trong M15 → multiplayer: SRP: Server spawn Civil Central + worker UTS theo team khi vào RtsNet_Game.
```

### File Game / Player liên quan

```
- **`Game/Startup/RtsNetworkSceneLoadHooksRegistration.cs`** - Trong M15 → multiplayer: Type RtsNetworkSceneLoadHooksRegistration.
- **`Player/LocalHumanOwnerMirrorBridge.cs`** - Trong M15 → multiplayer: DIP: Lobby Mirror → LocalHumanOwnerService + refresh presentation.
- **`Player/MpHudSuppliesResolver.cs`** - Trong M15 → multiplayer: SRP: Tìm component Supplies HUD đúng phe (Runtime UI UGUI vs (1)).
- **`Player/MpPlayerPresentationDirector.cs`** - Trong M15 → multiplayer: SRP: MP — mỗi client chỉ bật rig P1 hoặc P2 (fog + UI + PlayerInput riêng).
- **`Player/MpPlayerPresentationRig.cs`** - Trong M15 → multiplayer: SRP: Một bộ presentation MP — fog + HUD + PlayerInput cho P1 hoặc P2 (gắn trên scene RtsNet_Game).
- **`UI/Pregame/PregameMpLobbyCoordinator.cs`** - Trong M15 → multiplayer: SRP: Điều phối quyền lobby MP trên SSScene — client chỉ Ready/Back, host chọn map và Start.
```

---

## M16 — PvAI offline

**Mục tiêu:** Spawn base người chơi + bật AI khi không có Mirror.

**Phụ thuộc:** M1, M13.

### Thứ tự đọc gợi ý

1. ★ `PvAI/PvAiGameSceneBootstrap.cs`
2. ★ `PvAI/PvAiOfflineSpawnCoordinator.cs`
3. `PvAI/PvAiOfflineAiCoordinator.cs`
4. `PvAI/PregameAiDifficultyApplyService.cs`

### Danh sách file — `PvAI/` (13)

```
- **`PvAI/PregameAiDifficultyApplicator.cs`** - Trong M16 → PvAI offline: SRP: Hook scene Game 1 — gọi PregameAiDifficultyApplyService khi vào play mode. Có thể gắn cùng PvAiGameSceneBootstrap; không bắt buộc nếu bootstrap đã gọi service.
- **`PvAI/PregameAiDifficultyApplyService.cs`** - Trong M16 → PvAI offline: SRP: Áp PregameSessionState.SelectedDifficulty lên mọi AIController trong scene game offline.
- **`PvAI/PvAiDebugSessionLog.cs`** - Trong M16 → PvAI offline: Ghi NDJSON vào debug-559c4e.log (workspace root) khi kiểm tra PvAI ở Play Mode.
- **`PvAI/PvAiGameSceneBootstrap.cs`** - Trong M16 → PvAI offline: SRP: Khi Play map offline — chạy spawn PvE (không chạy khi Mirror active).
- **`PvAI/PvAiGameSceneSetup.cs`** - Trong M16 → PvAI offline: SRP: Cấu hình spawn offline PvE trên scene Game 1 (prefab + điểm xuất phát, không dùng CC cắm sẵn).
- **`PvAI/PvAiHealthFinding.cs`** - Trong M16 → PvAI offline: Type PvAiHealthFinding.
- **`PvAI/PvAiOfflineAiCoordinator.cs`** - Trong M16 → PvAI offline: SRP: Bật AI bot cho PvE offline — không tắt vĩnh viễn khi Prepare map cho MP.
- **`PvAI/PvAiOfflineEntityFactory.cs`** - Trong M16 → PvAI offline: SRP: Tạo bản instance offline từ prefab (gỡ Mirror nếu prefab từng wire MP).
- **`PvAI/PvAiOfflineSessionPrep.cs`** - Trong M16 → PvAI offline: SRP: Chuẩn bị session PvE offline và điều phối spawn căn cứ theo lần load scene.
- **`PvAI/PvAiOfflineSpawnCoordinator.cs`** - Trong M16 → PvAI offline: SRP: Spawn PvE offline khi bootstrap hoặc loading bar cần căn cứ Player1.
- **`PvAI/PvAiOfflineSpawnService.cs`** - Trong M16 → PvAI offline: SRP: Spawn Civil Central + unit khởi đầu cho human và AI khi Play PvE offline.
- **`PvAI/PvAiRuntimeHealthCheck.cs`** - Trong M16 → PvAI offline: SRP: Chạy kiểm tra PvAI sau khi bootstrap (Play Mode) và ghi báo cáo + debug log. Gắn trên root Game 1 hoặc object bootstrap; chỉ chạy một lần mỗi lần vào Play.
- **`PvAI/PvAiSceneValidator.cs`** - Trong M16 → PvAI offline: SRP: Kiểm tra invariant M5 PvAI (Game 1 offline) — dùng chung Editor và Play Mode.
```

---

## Phụ trợ chéo module (Utilities & khác)

**Mục tiêu:** Helper dùng chung — đọc khi trace từ module chính (grep reference).

### `Utilities/` (20)

```
- **`Utilities/AnimationConstants.cs`** - Trong phụ trợ → utility dùng chéo: Type AnimationConstants.
- **`Utilities/BuildingKindMatching.cs`** - Trong phụ trợ → utility dùng chéo: SRP: So khớp building với prefab archetype (hotkey A/S/D).
- **`Utilities/CivilCentralUtility.cs`** - Trong phụ trợ → utility dùng chéo: Identifies the main base (Civil Central) for win/lose and supply-deposit rules.
- **`Utilities/ClosestColliderComparer.cs`** - Trong phụ trợ → utility dùng chéo: Type ClosestColliderComparer.
- **`Utilities/ClosestCommandPostComparer.cs`** - Trong phụ trợ → utility dùng chéo: Type ClosestCommandPostComparer.
- **`Utilities/ClosestGameObjectComparer.cs`** - Trong phụ trợ → utility dùng chéo: Type ClosestGameObjectComparer.
- **`Utilities/CombatTargetGeometryUtility.cs`** - Trong phụ trợ → utility dùng chéo: Resolves closest surface points on combat targets (colliders, NavMeshObstacle footprint).
- **`Utilities/DamageableSensorAimUtility.cs`** - Trong phụ trợ → utility dùng chéo: Resolves world aim points on units (DamageableSensor when present).
- **`Utilities/HostileTargetLocator.cs`** - Trong phụ trợ → utility dùng chéo: Finds hostile IDamageable targets in range for defensive buildings.
- **`Utilities/InputSystemKeyboardUtility.cs`** - Trong phụ trợ → utility dùng chéo: SRP: Truy cập bàn phím qua Input System (không dùng UnityEngine.Input / KeyCode runtime).
- **`Utilities/PlacementFieldGridContext.cs`** - Trong phụ trợ → utility dùng chéo: Type PlacementFieldGridContext.
- **`Utilities/PlacementFieldGridUtility.cs`** - Trong phụ trợ → utility dùng chéo: SRP: Ánh xạ tọa độ thế giới → chỉ số ô trên lưới placement field.
- **`Utilities/PlacementFieldSelectionRegistry.cs`** - Trong phụ trợ → utility dùng chéo: SRP: AI — lưu địa chỉ đặt nhà đã chọn theo phe và tránh chọn trùng trong cùng ô field (bán kính tối thiểu). Người chơi dùng ghost + Restrictions trên Player.PlayerInput; không gọi registry này.
- **`Utilities/ProjectileArcMath.cs`** - Trong phụ trợ → utility dùng chéo: Shared parabolic arc math for unit/building projectiles.
- **`Utilities/StartingUnitSpawnEntry.cs`** - Trong phụ trợ → utility dùng chéo: SRP: Một dòng cấu hình spawn unit khởi đầu (prefab + số lượng).
- **`Utilities/StartingWorkerSpawnLayout.cs`** - Trong phụ trợ → utility dùng chéo: SRP: Tính vị trí spawn worker khởi đầu quanh điểm base (PvE / MP dùng chung).
- **`Utilities/SupplyDepositApproachUtility.cs`** - Trong phụ trợ → utility dùng chéo: SRP: Điểm tiếp cận động quanh Store/Civil Central — mỗi worker một ô trên vòng quanh footprint.
- **`Utilities/SupplyDepositLocator.cs`** - Trong phụ trợ → utility dùng chéo: Locates completed buildings where workers can deposit gathered supplies.
- **`Utilities/SystemCursorTextureBaker.cs`** - Trong phụ trợ → utility dùng chéo: Chuẩn hóa texture để dùng với UnityEngine.Cursor.SetCursor: asset Sprite/compressed thường không đọc được từ CPU và không đúng RGBA32/mip — Unity sẽ báo lỗi.
- **`Utilities/UnitKindMatching.cs`** - Trong phụ trợ → utility dùng chéo: SRP: So khớu hai unit có cùng archetype (double-click chọn cùng loại, v.v.).
```

### `Events/` — toàn bộ (18)

*(Nhiều event đã liệt kê trong module tương ứng; danh sách đầy đủ:)*

```
- **`Events/ActiveCommandChangedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Lệnh đang chờ xác nhận trên map (ghost / placement). Command = null khi hủy hoặc hoàn tất.
- **`Events/BuildingConstructStartedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Phát khi GameDevTV.RTS.Behavior.BuildBuildingAction bắt đầu (nhà được spawn / tiếp tục xây) — dùng để gỡ ghost đặt chỗ trên UI.
- **`Events/BuildingDeathEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/BuildingSpawnEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/CommandSelectedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/HotkeyTriggeredEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/PlaceholderDestroyEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/PlaceholderSpawnEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/SupplyDepletedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/SupplyEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/SupplySpawnEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitDeathEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitDeselectedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitLoadEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitSelectedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitSpawnEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UnitUnloadEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
- **`Events/UpgradeResearchedEvent.cs`** - Trong phụ trợ → toàn bộ event Bus: Payload Bus<T> theo Owner.
```

### `Units/` — data & interface còn lại (chưa gom vào M3–M5)

```
- **`Units/AbstractUnit.cs`** - Trong phụ trợ → SO/formation unit: Type AbstractUnit.
- **`Units/AbstractUnitSO.cs`** - Trong phụ trợ → SO/formation unit: ScriptableObject cau hinh du lieu.
- **`Units/UnitSO.cs`** - Trong phụ trợ → SO/formation unit: ScriptableObject cau hinh du lieu.
- **`Units/UnitCommands.cs`** - Trong phụ trợ → SO/formation unit: Type UnitCommands.
- **`Units/SightConfigSO.cs`** - Trong phụ trợ → SO/formation unit: ScriptableObject cau hinh du lieu.
- **`Units/SupplyCostSO.cs`** - Trong phụ trợ → SO/formation unit: ScriptableObject cau hinh du lieu.
- **`Units/Formation/GroupFormationMoveUtility.cs`** - Trong phụ trợ → SO/formation unit: SRP: Áp dụng Move cho nhóm unit theo formation vuông (player multi-select).
- **`Units/Formation/UnitFormationRole.cs`** - Trong phụ trợ → SO/formation unit: Vai trò xếp hàng — hàng trước (cận chiến/worker) vs hàng sau (archer).
- **`Units/Formation/UnitFormationRoleClassifier.cs`** - Trong phụ trợ → SO/formation unit: SRP: Phân loại unit vào hàng trước / hàng sau theo tên UnitSO.
- **`Units/Formation/UnitSquareFormationPlanner.cs`** - Trong phụ trợ → SO/formation unit: SRP: Tính vị trí đích hình vuông — hàng trước tiến hướng điểm click, archer hàng sau.
```

### Khác

```
- **`Gameplay/GameplayMapCoreMarker.cs`** - Trong phụ trợ chéo module: SRP: Đánh dấu root prefab lõi map (spawn PvE + PvP) — Editor tìm và cập nhật scene.
- **`MapTools/Editor/QuickPrefabScatterWindow.cs`** - Trong phụ trợ chéo module: Type QuickPrefabScatterWindow.
```

---

## Bảng tra nhanh: folder → module


| Folder `Assets/Scripts/` | Module chính           |
| ------------------------ | ---------------------- |
| `EventBus/`              | M0                     |
| `Game/`                  | M1                     |
| `Player/`                | M2, M6                 |
| `Commands/`              | M0, M2–M5, M8, M14     |
| `Units/`                 | M0, M3–M5, M8          |
| `Environment/`           | M3                     |
| `Behavior/`              | M17 (+ M3–M5)          |
| `Events/`                | Chéo (Bus subscribers) |
| `UI/`                    | M11                    |
| `Hotkeys/`               | M9                     |
| `SpeechRecognition/`     | M10                    |
| `Audio/`                 | M12                    |
| `Minimap/`               | M7                     |
| `AI/`                    | M13                    |
| `PvAI/`                  | M16                    |
| `Netplay/`               | M15                    |
| `TechTree/`              | M14                    |
| `Utilities/`             | Phụ trợ                |
| `Movement/`              | M2                     |
| `Gameplay/`, `MapTools/` | Phụ trợ                |


---

## Gợi ý học theo mục tiêu


| Mục tiêu            | Thứ tự module            |
| ------------------- | ------------------------ |
| Game chạy từ menu   | M0 → M1 → M2             |
| Worker / tài nguyên | M0 → M2 → M3 → M17       |
| Xây / train         | M0 → M3 → M4 → M14 → M11 |
| Combat              | M0 → M2 → M5 → M17       |
| Fog / minimap       | M0 → M1 → M6 → M7        |
| Voice               | M0 → M2 → M10            |
| AI                  | M0 → M3 → M4 → M5 → M13  |
| Multiplayer         | M0 → M1 → M2 → M15       |


---

## Liên kết tài liệu khác

- `docs/urts-architecture-map.md` — bản đồ kiến trúc tổng quan
- `docs/bang-anh-xa-lenh-giong-noi.md` — ánh xạ lệnh giọng nói
- `docs/HUONG_DAN_CHAY_AI.md` — chạy và test AI

