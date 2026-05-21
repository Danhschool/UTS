# Hướng dẫn chạy AI đối thủ (Petra + Command)

AI đối thủ dùng **cùng `BaseCommand`** như người chơi (`PlayerInput`), còn **Petra HQ** quyết định train/build/gather/attack.

## 1. Tạo ScriptableObject (Unity Editor)

Menu **RTS → AI → Create Data_Re AI Assets** tạo:

| File | Mô tả |
|------|--------|
| `Difficulty/AIDifficulty_Easy.asset` | Dễ |
| `Difficulty/AIDifficulty_Medium.asset` | Chuẩn |
| `Difficulty/AIDifficulty_Hard.asset` | Khó |
| `AIGameSessionConfig.asset` | Tham chiếu 3 difficulty; default = Medium |

`AIController` đọc `AIDifficultySO` (tick, attack threshold, worker cap, fog fair). Đổi runtime: `SetDifficulty(AIDifficultySO)` hoặc `SetDifficulty(session, AIDifficultyLevel)`.

### Gán trên `AIPetraConfig_Default`

- **Train**: Build Worker, Build Warrior/Archer/Knight (kéo từ `Data_Re/Buildings/Commands/`)
- **Build**: Build Store house, Barrack, Corral, Forge, Defense tower (kéo từ `Data_Re/Commands/`)
- **Research**: một `Research Infantry *` bất kỳ (Forge)
- **Building types**: BuildingSO tương ứng (Store, Civil Central, Barrack, Corral, Forge, Tower)

### Gán trên `AIDifficulty_*`

- Tick Interval: ~0.35s (Medium)
- Min Army Before Attack: 6
- Enable Attack: bật (tắt cho Sandbox)

## 2. Gắn vào scene

1. Tạo GameObject `AIBot`.
2. Add component **`AIBot`**.
3. **Ai Owner** = phe bot điều khiển (phải **trùng** `Owner` trên unit/building):
   - **AI1** — đối thủ (unit phe AI trên map).
   - **Player1** — xem AI chơi thay bạn (unit phe bạn); bật **Suppress Player Input When Controlling Player** để chuột không tranh lệnh với bot.
4. Gán: Petra Config, Difficulty, **Ground Layers**, **Damageable Layers**.
5. **Exploration Fog Reference** (khuyến nghị): kéo `MinimapFogSystemReference` từ scene (cùng logic explored RT như minimap). Scout đi vào ô **chưa explored**; để trống → patrol vòng quanh nhà như trước.
6. (Tuỳ chọn) Add **`AIDebugTelemetry`**, **`AISessionLogger`** và kéo vào `AIBot`.
7. Đảm bảo unit/building AI1 đã có trong scene hoặc spawn với `Owner = AI1`.

## 3. Log

- **Tần suất:** Mỗi **một pulse AI** (cách nhau `Tick Interval Seconds` trên `AIDifficulty`, mặc định ~**0,35s**) `AIBot` ghi một dòng **pulse** nếu bật trên `AIDebugTelemetry`. Mỗi lệnh gửi qua Dispatcher có thể ghi thêm một dòng nếu **Verbose Commands** bật.
- **Giảm nhanh log file/Console:**
  - Trên **`AIDebugTelemetry`:** tắt **Log Pulse Summaries** hoặc đặt **Pulse Summary Min Interval Seconds** > 0 (ví dụ 2–5); tắt **Verbose Commands** hoặc đặt **Command Log Min Interval Seconds** để giãn các dòng lệnh thành công.
  - Trên **`AISessionLogger`:** tắt **Enable File Log** để chỉ Console (hoặc ngược lại chỉnh **Log To Console** trên telemetry).
  - Trên **`AIBot`:** **Ai Tick Interval Override Seconds** > 0 (ví dụ 1s) để AI tick chậm hơn asset difficulty (ít pulse = ít dòng pulse mặc định).
- File: `Logs/AI/ai_AI1_*.log` (cạnh project).

## 3b. Gỡ lỗi: bám tường / chỉ xây Store

- **Scout bám tường:** trước đây có thể Move tới điểm **không nằm trên NavMesh**; agent bám obstacle. `AIExplorationManager` giờ **hạ xuống đất (Ground Layers) + `NavMesh.SamplePosition`**; nếu vẫn lỗi → kiểm tra **Bake NavMesh**, **`AIBot` Ground Layers**, và map có khe hẹp / cầu thang agent không qua được.

- **Không xây nhà ngoài Store (AI):** (1) **`AIPetraConfig`**: gán đủ `BuildBarracksCommand` / Corral / Forge / Tower; mỗi `BuildBuildingCommand` phải trỏ **BuildingSO** có **Tech Tree** và **đã unlock** cho `Owner` bot (`BuildBuildingCommand.CanHandle` và `AIBuildStructureQueuePlan.CanEnqueue`). (2) **Đặt nhà**: `TryFindPlacement` + `Restrictions`/`AllRestrictionsPass` — nhà footprint lớn cần chỗ trống; nhỏ storefront thường dễ hơn. (3) **Thứ tự infra:** barracks có thể bị chờ corral+forge; nếu hai nhà đó mãi không xong (tech/placement/thiếu tài nguyên), sau **90 giây `GameTime`** bot **vẫn enqueue** barracks/tháp để không kẹt vĩnh viễn.

- **Xây 4–5 Store liên tục:** thường do **`StoreBuildingType` không trùng `BuildingSO`** của nhà kho trên map → `Stores.Count` vẫn 0. Code dùng **`EffectiveStoreType`** (ưu tiên field, fallback `BuildStoreCommand.Building`) và **`Max Store Buildings`** (Inspector, mặc định **1**). Vẫn nên gán **Building types** trùng thật với prefab.

## 4. Sandbox test (`AI_Sandbox`)

Menu **RTS → AI → Create AI Sandbox Scene** (nhân từ `Game 1.unity`). Checklist đầy đủ: **`docs/AI_SANDBOX_CHECKLIST.md`** (gồm win/lose Civil Central).

Tóm tắt:

- [ ] Worker gather → return Store / Civil Central
- [ ] Train Worker tại Civil Central
- [ ] Build Store (đường về ngắn hơn)
- [ ] Build Barrack → train Warrior
- [ ] Build Forge → research 1 upgrade
- [ ] Defense tower tự bắn (không cần lệnh AI)
- [ ] Corral sinh food (passive)
- [ ] Scout / attack wave khi đủ quân; lính Stop patrol nhẹ để mở fog
- [ ] AI **không** dùng Stop / Cancel Building

## 5. Kiến trúc ngắn

```
AIBot → AIHeadquarters (pulse)
  → Defense / Queue (build, train, research) / Attack / Bases (gather) / Explore (idle military)
  → AICommandSession → AICommandDispatcher → BaseCommand.Handle
```

**Kinh nghiệm & pitfall:** `docs/KINH_NGHIEM_AI.md`

**Economy-only (sandbox):** `AIController` trên scene → `Assets/Scripts/AI/` (`AIEconomyManager`, registry, dispatcher). Cùng nguyên tắc command + BT; không thay Petra HQ.

Worker đang **build** hoặc **gather/return** sẽ không nhận lệnh mới (tránh hủy công trình).

**Worker xây dedi (`AIPetraConfig`):** mặc định có **Dedicated construction worker**: khi còn **≥ MinWorkersBeforeDedicated**, một worker gần HQ được **ưu tiên** cho lệnh đặt nhà; vẫn có thể gather nếu chưa có lệnh build hợp lệ. Tắt bằng **Use Dedicated Construction Worker** trong Petra Config (Inspector).

**Gather & fog:** AI chỉ `Gather` khi mỏ **IsVisible**. Mỏ chưa thấy → `MoveTo`. **Phân bổ worker theo loại mỏ:** tỉ lệ mục tiêu **gỗ : đá : thịt = 1 : 1 : 1** (ví dụ 6 worker → 2 / 2 / 2; 5 worker → 2 / 2 / 1 do phần dư chia lần lượt **gỗ → đá → thịt**). Worker rảnh / rebalance chọn loại còn **thiếu slot** trước; kho một loại “dư” vẫn có thể được khai thác nếu **số worker loại đó chưa đủ slot**. **Cân bằng kho:** khi có surplus+deficit trong kho vẫn dùng `TryRebalanceOverflow`; thêm `TryRebalanceOverQuota` để kéo worker khỏi loại đông quá slot. Mỗi vòng quét **cả** gỗ/đá/thịt trong bán kính (`DiscoverAllSupplyKinds`). Tối đa 12 vòng rebalance; sau đó gán worker **Stop** tối đa 6/pulse. **Thịt:** ưu tiên mỏ food, không có mỏ → `Attack` WildAnimal. **ReturnSupplies:** behavior graph tự xử lý.

**Train quân đánh:** enqueue khi có **Barracks** và đủ **`MinWorkersBeforeArmy`**. Lệnh train trên Barracks phải **ghi nhận được** khi nằm trong **`OverrideCommandsCommand`** (giống unit): `AvailableCommandsResolver.GetFlattened` được dùng cho nhà. **`PickProducer`** kiểm khóa bằng **`BuildUnitCommand`** thật trên Barracks; dispatch dùng đúng asset đó (không fallback train quân vào **Main Building**). **`AITrainUnitQueuePlan.CanEnqueue`** kiểm **đủ tài nguyên + tech unit đã unlock** (`Unit.TechTree`).

**Lính rảnh / đã đến đích Move** (không phải Worker, không đang Attack): ưu tiên `Move` tới ô **chưa explored** (fog camera trên `AIBot`); fallback patrol quanh HQ với bán kính **đáng kể xa hơn Gather/Defense** (~2×–~9× base). Cooldown scout ngắn hơn; **3 unit/pulse** (Late **4**).

**Infrastructure sau warehouse:** Corral → Forge được đưa vào queue sớm; **Barracks / tháp** ưu tiên khi offense hoặc khi đã có corral+forge; nếu backbone mãi chưa xong (placement/tech) thì sau **90s GameTime** vẫn enqueue barracks/tháp để tránh deadlock. Forge dùng queue `forge_building` (tách `research`). Còn lại như Ưu tiên queue offense vs economy và tăng lượt queue khi thiếu infra cốt lõi.

**Phát triển đầu game:** enqueue dân / nhà thường xuyên khi có thể; số **lượt queue** trong một pulse tăng khi `HasStrongEconomyForBuilding`; kho thấp vẫn có fallback gather; `TryPickEconomyGoal` có `FindNearestRelaxed` khi thiếu mỏ.
