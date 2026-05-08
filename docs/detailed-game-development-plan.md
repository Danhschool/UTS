# Detailed Development Plan (AoE-style RTS)

Tài liệu này là kế hoạch triển khai chi tiết để phát triển game RTS riêng của bạn dựa trên URTS hiện tại, có tham chiếu cách làm từ repo top-down shooter.

## Repos tham khảo

- URTS gốc (repo hiện tại): [GameDevTV URTS Course](https://gitlab.com/GameDevTV/unity-real-time-strategy/urts-course.git)
- Shooter tham khảo: [Danhschool Top-DownShooter](https://github.com/Danhschool/Top-DownShooter)

## Mục tiêu tổng thể

- Xây một game RTS có loop hoàn chỉnh: gather -> build -> train -> research -> combat -> victory/defeat.
- Giữ kiến trúc SOLID, data-driven bằng ScriptableObject, event-driven bằng `Bus<T>`.
- Dễ mở rộng thêm content (unit/building/upgrade/faction) và sẵn sàng cho multiplayer sau này.

---

## Phase 0 - Project Setup and Baseline (1-2 ngày)

## Mục tiêu

- Khóa chuẩn kiến trúc, docs, và quy trình làm việc.

## Công việc

1. Chuẩn hóa tài liệu trong `docs`:
   - `README.md`
   - `survey-summary.md`
   - `urts-architecture-map.md`
   - `repo-migration-checklist.md`
   - `new-repo-quickstart.md`
2. Xác nhận folder kiến trúc dưới `Assets/Scripts` theo domain.
3. Xác nhận rules coding:
   - SOLID
   - Không dùng `Find*` trong runtime logic
   - Cache component trong `Awake/Start`
4. Tạo 1 scene `Sandbox_Baseline` để smoke test input/selection/command.

## Done criteria

- Team mở `docs/README.md` là hiểu flow dự án trong <10 phút.
- Scene baseline chạy được select + move đơn vị.

---

## Phase 1 - Input, Camera, Selection, Command Core (3-5 ngày)

## Mục tiêu

- Hoàn chỉnh lõi tương tác người chơi.

## Công việc

1. Input System:
   - Chuẩn hóa action map cho camera, select, command.
   - Mapping hotkeys cơ bản (Stop, Attack Move, Build menu).
2. Camera RTS:
   - Pan/zoom/rotate mượt, clamp boundary.
3. Selection:
   - Click select 1 unit/building.
   - Drag box multi-select.
4. Command activation:
   - UI chọn command -> `CommandSelectedEvent` -> `PlayerInput`.
   - Right-click context command (move/attack/gather).
5. Formation movement:
   - Cải thiện spacing theo `UnitIndex` trong `CommandContext`.

## Tham chiếu code

- URTS: `Assets/Scripts/Player/PlayerInput.cs`, `Assets/Scripts/Commands/BaseCommand.cs`
- Top-DownShooter: tham khảo pattern tách Input khỏi combat/weapon controller.

## Done criteria

- Người chơi điều khiển camera + select + ra lệnh ổn định.
- Không có logic command nằm trực tiếp trong UI component.

---

## Phase 2 - Combat Foundation and Game Feel (5-7 ngày)

## Mục tiêu

- Combat ổn định, dễ mở rộng, có cảm giác đánh tốt.

## Công việc

1. Chuẩn hóa damage flow:
   - `IAttacker` -> `IDamageable.TakeDamage()`.
   - Chết unit/building phát đúng event.
2. Attack config:
   - Chuẩn hóa `AttackConfigSO` cho melee/range/AOE.
3. Targeting:
   - Dùng `DamageableSensor` + visibility để lọc target hợp lệ.
4. FX layer tách riêng:
   - Hit VFX/SFX, death VFX/SFX nghe event, không nhúng vào game logic.
5. Optional projectile:
   - Nếu cần, thêm object pool cho projectile/effects.

## Tham chiếu code

- URTS: `Assets/Scripts/Units/DamageableSensor.cs`, `Assets/Scripts/Behavior/AttackTargetAction.cs`
- Top-DownShooter: tham khảo cấu trúc weapon/hit/FX (controller tách khỏi health).

## Done criteria

- Unit đánh đúng mục tiêu, dừng đúng lúc, không đánh xuyên fog visibility.
- Tách rõ gameplay logic và presentation FX.

---

## Phase 3 - Resource Economy (5-7 ngày)

## Mục tiêu

- Loop tài nguyên hoạt động hoàn chỉnh.

## Công việc

1. Resource types:
   - Chuẩn hóa `SupplySO` cho Food/Wood/Gold/Stone (hoặc set bạn chọn).
2. Gather nodes:
   - Tạo/điều chỉnh prefab `GatherableSupply`.
3. Worker gather loop:
   - Move -> gather -> return command post -> update supply.
4. Resource state:
   - Tách dần model tài nguyên khỏi UI (`Supplies` theo hướng service + observer).
5. Resource HUD:
   - UI chỉ lắng nghe event/state change, không chứa gameplay logic.

## Tham chiếu code

- URTS: `Assets/Scripts/Environment/GatherableSupply.cs`, `Assets/Scripts/Player/Supplies.cs`, `Assets/Scripts/Commands/GatherCommand.cs`
- Top-DownShooter: tham khảo pattern “state component + UI hiển thị” (không hardcode UI vào gameplay).

## Done criteria

- Sandbox resource chạy ổn với 1 TC + 3 worker + nhiều node.
- UI tài nguyên cập nhật đúng khi gather/build/train/research.

---

## Phase 4 - Buildings and Construction (7-10 ngày)

## Mục tiêu

- Hệ thống đặt nhà và xây nhà chắc chắn.

## Công việc

1. Building data:
   - Chuẩn hóa `BuildingSO` (cost, buildTime, HP, unlocks, populationProvided).
2. Placement:
   - Ghost preview đỏ/xanh.
   - Validate collision/navmesh/surface qua `BuildingRestrictionSO`.
3. Construction:
   - Worker build/resume/cancel qua `IBuildingBuilder`.
4. Building states:
   - `BuildingProgress` + UI progress.
5. Events:
   - Spawn/death/placeholder events chạy đúng cho UI/Fog/Tech.

## Tham chiếu code

- URTS: `Assets/Scripts/Commands/BuildBuildingCommand.cs`, `Assets/Scripts/Units/BaseBuilding.cs`
- Top-DownShooter: tham khảo UX feedback (confirm/cancel audio/visual cues).

## Done criteria

- Build loop end-to-end ổn định trong sandbox.
- Không trừ tài nguyên sai khi cancel/fail placement.

---

## Phase 5 - Production Queue and Tech Tree (7-10 ngày)

## Mục tiêu

- Train unit + research upgrade có dependency rõ ràng.

## Công việc

1. Queue system:
   - Kiểm tra `BaseBuilding.Queue`, add/cancel item, progress update.
2. Unlockable data:
   - Chuẩn hóa `UnlockableSO` và dependencies.
3. Tech logic:
   - `TechTreeSO.IsUnlocked/IsResearched/GetUnmetDependencies`.
4. Upgrade effects:
   - Áp dụng `UpgradeSO` lên `AbstractUnitSO` clone runtime.
5. Upgrade UI:
   - Hiển thị lock reasons + unmet dependencies.

## Tham chiếu code

- URTS: `Assets/Scripts/TechTree/TechTreeSO.cs`, `Assets/Scripts/Commands/ResearchUpgradeCommand.cs`, `Assets/Scripts/Events/UpgradeResearchedEvent.cs`
- Top-DownShooter: tham khảo tư duy tiến trình nâng cấp vũ khí/nhân vật (nếu có).

## Done criteria

- Nghiên cứu xong upgrade làm thay đổi stat thực sự.
- Không research được item chưa đủ điều kiện.

---

## Phase 6 - Population and Unit Ownership (3-5 ngày)

## Mục tiêu

- Quản lý dân số và ownership theo faction.

## Công việc

1. Population model:
   - current/max population theo Owner.
2. Gate train logic:
   - Không cho train khi vượt max pop.
3. House/towncenter:
   - Tăng `PopulationLimit`.
4. Ownership audit:
   - Đảm bảo event/resource/tech đều key đúng `Owner`.

## Tham chiếu code

- URTS: `Assets/Scripts/Units/Owner.cs`, `Assets/Scripts/Player/Supplies.cs`
- Top-DownShooter: không trực tiếp, dùng tư duy tách player-state khỏi view.

## Done criteria

- UI hiện đúng `current/max`.
- Multi-owner (Player + AI) không lẫn state.

---

## Phase 7 - AI Opponent (10-14 ngày)

## Mục tiêu

- Có AI chơi được 1 trận hoàn chỉnh.

## Công việc

1. Macro AI controller:
   - Quyết định build/train/attack theo phase.
2. Execution layer:
   - AI gửi lệnh qua command system như player (không hack state).
3. Economy behavior:
   - Ưu tiên worker + nhà cốt lõi + quân.
4. Combat behavior:
   - Gom quân tối thiểu và tấn công căn cứ địch.
5. Difficulty knobs:
   - Tham số gather rate, build priority, aggression.

## Tham chiếu code

- URTS: `Assets/Scripts/Behavior/*`, `Assets/Scripts/Commands/*`
- Top-DownShooter: tham khảo AI micro/combat feel nếu có.

## Done criteria

- AI tự chơi full loop tới thắng/thua trong sandbox match.

---

## Phase 8 - Fog, Minimap, UX, Optimization (7-12 ngày)

## Mục tiêu

- Nâng chất lượng trải nghiệm và ổn định hiệu năng.

## Công việc

1. Fog of war:
   - Hoàn thiện `FogVisibilityManager`, kiểm tra hide/show edge cases.
2. Minimap:
   - Render texture + icon units/buildings theo owner.
3. UX polish:
   - Selection feedback, command feedback, tooltip quality.
4. Performance pass:
   - Giảm LINQ ở hot path.
   - Pool VFX/projectile.
   - Giảm allocations trong update loops.

## Tham chiếu code

- URTS: `Assets/Scripts/Player/FogVisibilityManager.cs`, `Assets/Scripts/UI/*`
- Top-DownShooter: tham khảo game feel + FX pacing.

## Done criteria

- Trận 15-20 phút không tụt hiệu năng nghiêm trọng.
- UX đủ rõ để người chơi mới hiểu ngay command/state.

---

## Phase 9 - Vertical Slice and Production Discipline (5-8 ngày)

## Mục tiêu

- Chốt 1 vertical slice hoàn chỉnh để từ đó nhân rộng content.

## Công việc

1. Chốt 1 faction:
   - 1 worker, 3-4 military units, 4-6 buildings, 8-12 upgrades.
2. Chốt 1 map:
   - Resource distribution + choke points cơ bản.
3. Chốt win/lose:
   - Defeat khi mất nhà chính, victory khi hạ nhà chính địch.
4. Regression checklist:
   - Chạy lại sandbox cho resource/build/tech/combat trước mỗi merge lớn.
5. Backlog phân loại:
   - Must-have / Should-have / Nice-to-have.

## Done criteria

- Có bản chơi được từ đầu tới cuối trận.
- Không có blocker kiến trúc cho việc thêm content mới.

---

## Sandbox scenes bắt buộc đề xuất

- `Sandbox_InputSelection`
- `Sandbox_Combat`
- `Sandbox_ResourceLoop`
- `Sandbox_BuildingPlacement`
- `Sandbox_ProductionTech`
- `Sandbox_AI_1v1`

Mỗi sandbox cần có checklist test riêng và expected result rõ ràng.

---

## Kế hoạch nén 2 tuần (bản thực chiến)

Mục tiêu 2 tuần là ra được **vertical slice chơi được** (không full feature):
- Có loop: gather -> build -> train -> combat -> thắng/thua cơ bản.
- Tạm **hoãn**: minimap nâng cao, AI chiến lược phức tạp, polish sâu, quá nhiều content.

### Tuần 1 (Foundation + Core Loop)

#### Ngày 1-2

- Chốt Phase 0 + phần quan trọng Phase 1:
  - Input/camera/selection ổn định.
  - `CommandSelectedEvent` -> `PlayerInput` -> `BaseCommand.Handle` hoạt động end-to-end.
  - Tạo `Sandbox_InputSelection`.

#### Ngày 3-4

- Chốt phần lõi Phase 2:
  - Damage flow và target selection (`DamageableSensor`) ổn định.
  - Combat cơ bản cho 1 melee + 1 ranged unit.
  - Tạo `Sandbox_Combat`.

#### Ngày 5-7

- Chốt phần lõi Phase 3:
  - Worker gather/return vòng kín.
  - UI tài nguyên cập nhật đúng theo `SupplyEvent`.
  - Tạo `Sandbox_ResourceLoop`.

### Tuần 2 (Buildings + Tech Lite + Vertical Slice)

#### Ngày 8-10

- Chốt phần lõi Phase 4:
  - Building placement (ghost + restriction) chạy ổn.
  - Worker build/resume/cancel chạy đúng.
  - Tạo `Sandbox_BuildingPlacement`.

#### Ngày 11-12

- Chốt phần tối thiểu Phase 5 + 6:
  - Queue train unit cơ bản.
  - 2-3 upgrade cốt lõi (ví dụ +damage, +armor, +gather speed).
  - Population gate đơn giản (current/max).
  - Tạo `Sandbox_ProductionTech`.

#### Ngày 13-14

- Chốt phần tối thiểu Phase 9:
  - 1 map, 1 faction, 4-6 building, 3-4 unit, 1 điều kiện thắng/thua.
  - Regression tất cả sandbox trước khi khóa bản.
  - Đóng gói bản chơi thử nội bộ.

### Scope cắt giảm bắt buộc để kịp 2 tuần

- AI đối thủ chỉ mức rất đơn giản hoặc tạm dùng script hành vi tĩnh.
- Chưa làm minimap hoàn chỉnh.
- Chưa tối ưu sâu hiệu năng (chỉ fix điểm nóng rõ ràng).
- Chưa mở rộng nhiều faction/age/content.

### Definition of Done cho mốc 2 tuần

- Người chơi có thể vào trận và hoàn thành loop cơ bản từ đầu đến cuối.
- Không có bug blocker ở command/resource/build/combat/tech lite.
- 5 sandbox quan trọng pass:
  - `Sandbox_InputSelection`
  - `Sandbox_Combat`
  - `Sandbox_ResourceLoop`
  - `Sandbox_BuildingPlacement`
  - `Sandbox_ProductionTech`
- Có thể demo nội bộ liên tục 10-15 phút mà không crash.

---

## KPI kiểm soát chất lượng

- Command response delay < 100ms trong cảnh benchmark.
- Không có `Find*` trong runtime gameplay scripts.
- Không gọi `GetComponent<T>()` trong `Update/FixedUpdate/LateUpdate`.
- Tỷ lệ pass sandbox regression >= 90% trước merge.
- Mỗi feature mới có:
  - data asset (`ScriptableObject`) tương ứng
  - event flow rõ ràng
  - UI phản hồi đúng state

---

## Phụ lục – Các bước triển khai bám sát cấu trúc khoá học

Phần này gom lại nội dung lớn của khoá học Udemy thành các **giai đoạn thực thi** tương ứng, để bạn có thể làm game tương tự mà không cần bám chặt theo từng video.

### Giai đoạn A – Camera Top‑Down & NavMesh

- **A.1. Camera & di chuyển bàn phím**
  - Tạo camera top‑down cố định độ cao tương đối.
  - Thêm script pan bằng WASD, zoom bằng scroll, rotate bằng chuột/phím.
  - Thêm mouse edge panning, clamp vị trí camera trong level bounds.
- **A.2. NavMesh & NavMeshAgent**
  - Bake NavMesh cho ground, đánh dấu areas & obstacles.
  - Thêm `NavMeshAgent` cho prefab unit, thử lệnh move đơn giản.

### Giai đoạn B – Selection, Event Bus, Multiple Units

- **B.1. Unit selection cơ bản**
  - Interface `ISelectable`, decal/outline highlight.
  - Click trái → raycast → chọn đơn lẻ.
- **B.2. Event Bus cho selection**
  - Tạo `IEvent` + `Bus<T>`.
  - `UnitSelectedEvent` / `UnitDeselectedEvent` → `PlayerInput` + `RuntimeUI` lắng nghe.
- **B.3. Drag select & multi‑select**
  - Vẽ selection box bằng UI `RectTransform`.
  - Collect các collider unit trong vùng box → add vào selection.
  - Thêm logic “add/remove” unit khỏi selection (Shift+click nếu muốn).
- **B.4. Prevent unit dancing**
  - Khi move nhiều unit, offset điểm đến dựa trên index → đội hình.
  - Điều chỉnh avoidance/priorities trên `NavMeshAgent`.

### Giai đoạn C – Buildings, ScriptableObject & Commands

- **C.1. Building & unit SOs**
  - Tách `AbstractUnitSO` → `UnitSO` và `BuildingSO` (theo đúng code URTS).
  - Mỗi prefab unit/building tham chiếu tới SO tương ứng để chứa stat/cost.
- **C.2. Command pattern**
  - `BaseCommand` + `CommandContext`.
  - Implement:
    - Move command
    - Build Unit command (train từ building)
  - Thêm UI actions hiển thị các command lưu trên unit/building.
- **C.3. Queue & Progress**
  - `BaseBuilding.Queue` cho build unit.
  - UI progress bar cho từng slot, huỷ item trong queue.

### Giai đoạn D – Behavior Trees & Resource Gathering

- **D.1. Behavior tree cho unit**
  - Tree cơ bản: Idle → MoveTo → PerformAction (gather/attack).
  - Node custom: MoveToTargetLocation, GatherSupplies, ReturnSupplies.
- **D.2. Gatherable Supplies & Player Supplies**
  - `SupplySO`, `GatherableSupply`, `IGatherable`.
  - Event `SupplyEvent`, `SupplySpawnEvent`, `SupplyDepletedEvent`.
  - `Supplies` lắng nghe event và cập nhật tài nguyên + HUD.
- **D.3. Worker commands**
  - Command Gather:
    - Click phải lên `GatherableSupply` → worker đi gather.
  - Return Supplies:
    - Worker tự tìm command post gần nhất để trả tài nguyên.

### Giai đoạn E – Building System nâng cao & UI

- **E.1. Building placement**
  - Ghost placement (material đổi màu Đỏ/Xanh).
  - `BuildingRestrictionSO` để check overlap, khoảng cách, fog, v.v.
- **E.2. Construction behavior**
  - Worker tree:
    - Move tới vị trí nhà → play animation build → tick `BuildingProgress`.
  - Xử lý edge case: cancel, resume, builder chết, v.v.
- **E.3. UI cho building**
  - Menu unit/building riêng.
  - Under‑construction UI, build queue UI, lock/unlock command với tooltip.

### Giai đoạn F – Military Units, Combat, Transport

- **F.1. Combat cơ bản**
  - `DamageableSensor` để sense kẻ địch.
  - `AttackConfigSO` cấu hình damage, tốc độ bắn, range, AOE.
  - `AttackCommand` + `AttackMove`:
    - Click vào enemy → tấn công.
    - Click vào ground với Attack‑Move → move, ưu tiên tấn công kẻ gặp trên đường.
- **F.2. Unit đặc biệt**
  - Grenadier:
    - Projectile + AOE damage.
  - Air Transport:
    - Config capacity (`TransportConfigSO`).
    - Command & behavior load/unload, UI transport.

### Giai đoạn G – Ownership, Tech Tree, Fog, Minimap, Polish

- **G.1. Ownership**
  - `Owner` enum, các event/resource/tech key theo Owner.
  - Supplies per owner, UI theo Owner hiện tại.
- **G.2. Tech Tree & Upgrades**
  - `TechTreeSO`, `UnlockableSO`, `UpgradeSO` + modifiers.
  - Research upgrade từ building, apply upgrade lên `AbstractUnitSO`.
- **G.3. Fog of War & Minimap**
  - `FogVisibilityManager`, `IHideable`, sight radius theo `SightConfigSO`.
  - Minimap: render texture + icon unit/building, điều khiển camera qua click/right‑click.
- **G.4. Polish & fix bug**
  - Population cost/supply, control groups, hotkeys, rally points, click indicator, mining juice.


