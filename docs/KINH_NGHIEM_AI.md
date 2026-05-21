# Kinh nghiệm làm AI RTS (Petra) — tổng hợp đến hiện tại

Tài liệu gom lại những gì đã làm, vỡ, và sửa trong project UTS — không phải lý thuyết RTS chung.

**Chạy & cấu hình:** `docs/HUONG_DAN_CHAY_AI.md`  
**Asset mẫu:** `Assets/Data_Re/AI/`  
**Menu:** RTS → AI → Create Data_Re AI Assets

---

## 1. Nguyên tắc thiết kế (nên giữ)

### Dùng lại gameplay của người chơi
- AI **không** viết lại gather/build/attack; gửi **`BaseCommand`** qua dispatcher → cùng đường với `PlayerInput`.
- **Lợi:** hành vi đồng nhất, ít bug “AI khác người”; **Rủi ro:** mọi lỗi blackboard/BT/NavMesh của unit cũng ảnh hưởng AI.

### Tách “suy nghĩ” và “ra lệnh”
```
AIBot (tick ~0.35s)
  → AIHeadquarters (một pulse)
       Defense → Enqueue queue → Attack → Bases (gather) → Explore
  → AICommandSession / Dispatcher → BaseCommand.Handle
```
- **Petra / HQ:** *cái gì* làm tiếp (train, build, tấn công, rebalance gather).
- **Command layer:** *ai* nhận lệnh, *lệnh nào* được phép (`AICommandPolicy`, `AICommandResolver`).

### Data-driven trước code
- `AIPetraConfigSO`: personality, queue priority, command SO (Build Store, Barracks…).
- `AIDifficultySO`: tick, army threshold, fog fairness.
- **Inspector sai một `BuildingSO`** → hành vi sai cả hệ (ví dụ `Stores.Count` = 0 → spam Store).

---

## 2. Kiến trúc lớp (SOLID thực tế)

| Lớp | Trách nhiệm |
|-----|-------------|
| `AIWorldState` / `AIUnitRegistry` | Đếm worker, store, barracks; không quét scene mỗi frame bừa bãi |
| `AIResourceMemory` / `AIEnemyMemory` | Kho, mỏ, địch (có fair fog) |
| `AIQueueManager` + `IQueuePlan` | Intent xây/train/research; sort theo priority |
| `AIBasesManager` | Phân worker gather / rebalance |
| `AIAttackManager` / `AIDefenseManager` / `AIExplorationManager` | Quân, scout |
| `AIBuildingPlacementService` | Đặt nhà (NavMesh + restrictions) |
| `Worker` + Behavior Graph | Thực thi gather/return/build sau khi nhận lệnh |

**DIP:** HQ phụ thuộc abstraction (`IQueuePlan`, `AIPetraContext`), không gọi thẳng UI.

### Layer economy thử nghiệm (`Assets/Scripts/AI/`)

Dùng khi chưa gắn full Petra trên scene:

| Thành phần | Vai trò |
|------------|---------|
| `AIController` | Tick snapshot → `AIEconomyManager` → `AICommandDispatcher` |
| `AIEconomyManager` | Gather 1:1:1, Store xa; **không** spam return |
| `AIEconomyConfigResolver` | Tự suy khoảng cách / loại mỏ từ map |
| `AIWorkerCommandGuard` | Không gán lệnh khi build / chu kỳ gather |
| `WorkerGatherAssignmentLock` | Một `Gather` / node; BT loop phía sau |

Cùng nguyên tắc Petra: command thật, blackboard sync khi đổi lệnh, fog = `IsVisible`.

---

## 3. Một pulse AI chạy thế nào

Thứ tự trong `AIHeadquarters.Update` (quan trọng khi debug):

1. **Defense** — ưu tiên cao nhất.
2. **`EnqueueEconomyPlans`** — chỉ *thêm* plan vào queue (chưa xây ngay).
3. **Nhiều vòng `queues.Update`** — mỗi vòng thực thi **một** plan priority cao nhất pass `CanEnqueue` + `TryExecute`. Số vòng tăng khi early game / thiếu infra.
4. **Attack** — khi đủ quân + offense mode.
5. **Bases (gather)** — worker rảnh; **không** gán lại worker đang build / mang hàng (policy).
6. **Explore** — lính rảnh → fog chưa explored.

**Building “tiếp theo”** = plan **priority cao nhất trong pending** tại pulse đó, không phải FIFO. Mặc định: Dropsite 400 > Military 120 > Forge 110 > Corral 45 > Tower 40; offense mode bump barracks/tower.

**Sau có warehouse:** burst Corral → Forge → Barracks (khi offense *hoặc* đủ corral+forge *hoặc* ≥ 90s game) → Tower.

---

## 4. Kinh nghiệm kinh tế / gather

| Bài học | Chi tiết |
|---------|----------|
| **Fog = visibility** | Chỉ `Gather` khi mỏ `IsVisible`; chưa thấy → `MoveTo`. |
| **Return để BT xử lý** | Planner **không** gọi `ReturnSupplies` liên tục; graph tự return khi đầy. |
| **Một Gather / node** | `WorkerGatherAssignmentLock` + `Gather()` no-op cùng node; đổi mỏ khác vẫn dispatch. |
| **Cân bằng 1:1:1** | Gỗ / đá / thịt theo slot; rebalance overflow/quota (Petra `AIBasesManager`). |
| **Thịt** | Mỏ food → không có → `Attack` WildAnimal. |
| **Dedicated builder** | Một worker ưu tiên build khi đủ `MinWorkersBeforeDedicated`. |
| **Không interrupt build** | Policy: không Stop/CancelBuilding; không gán lệnh mới khi `IsBuilding`. |

---

## 5. Lỗi hay gặp & cách nhận ra

### Config / world state
- **`StoreBuildingType` ≠ BuildingSO thật** → `Stores.Count` = 0 → xây Store mãi. Dùng `EffectiveStoreType` + `MaxStoreBuildings`.
- **Tech chưa unlock** → plan enqueue nhưng `CanEnqueue` fail im lặng → cần log pulse / kiểm Inspector.
- **Placement** → barracks/forge không bao giờ xong → backbone kẹt; bypass **90s** `GameTime`.

### Command / producer
- Train quân trên Barracks nằm trong **`OverrideCommandsCommand`** → cần **`GetFlattened`** trong `AvailableCommandsResolver`.
- **`PickProducer`** phải trỏ đúng `BuildUnitCommand` trên Barracks, **không** fallback Main Building.

### Worker / blackboard (quan trọng)
- AI gán `Supply` nhưng **không** gán `SupplySO` → Return/deposit sai (gỗ + SO đá).
- **Fix đúng chỗ:** `SyncGatherBlackboard` tại `Worker.Gather`, `HandleGatherSupplies`, behavior `GatherSuppliesAction` — **không** poll mỗi frame trong `Update`.
- **`Supply` vs `GatherableSupplies`** trên BT: phải đồng bộ cả hai.

### NavMesh / movement
- Scout target ngoài NavMesh → bám tường → **`NavMesh.SamplePosition`** + Ground Layers (`AIExplorationManager`).
- Sửa `MoveTo*` quá “thông minh” (lock goal, `isStopped` sớm) → **phá** gameplay; đã revert về bản git ổn định.

### Unit kẹt trạng thái
- `Command = Attack` nhưng target chết → scout/AI đứng im → `AbstractUnit.Update` + release attack (trade-off hiệu năng).
- Worker săn thú xa → `TryReleaseStaleWildAnimalAttack`.
- Sensor gán target khi không phải Attack → worker đánh spawn → **tắt auto-target** trên worker.

### Log
- Mỗi pulse + mỗi command = file log phình nhanh → throttle trên `AIDebugTelemetry`, tăng tick override trên `AIBot`.

---

## 6. Hiệu năng & code smell

| Nên | Tránh |
|-----|--------|
| Tick AI 0.35–1s | Logic chiến lược trong `Update` từng unit |
| Registry/world state cập nhật theo event | `FindObjectOfType` / `GameObject.Find` trong AI |
| Sync blackboard **khi đổi lệnh** | `TryRepair…` mỗi frame trên mọi worker |
| Cache component trong `Awake` | `GetComponent` trong `Update` |
| Giảm log (pulse interval, verbose off) | Ghi file mỗi command thành công |

**Quy tắc project:** cấm `Find*` trong code AI; cache component; tách SRP — áp dụng ở `Assets/Scripts/AI/` và Petra.

---

## 7. Debug có hệ thống

1. **Pulse string** (`LastPulse`): `defense+queue+gather`, `queue:dropsite`, …
2. **Sandbox** (`docs/HUONG_DAN_CHAY_AI.md` §4): gather→return, 1 store, barracks→train, forge→research, scout fog.
3. **Inspector worker đang lỗi:** `Command`, `Supply`, `SupplySO`, `SupplyAmountHeld`, `StoreSO`, `CommandPost`.
4. **Config warning** trên `AIBot` khi thiếu reference command/building.

---

## 8. Những gì còn “mỏng” / việc sau

- Folder `Planners/` cũ vs Petra mới — tránh trộn hai kiến trúc.
- `AbstractUnit.Update` poll attack — nên dần chuyển sang event (target die, sensor exit).
- Return anim đứng im có thể còn do **graph** (không sửa `.asset` trừ khi bạn yêu cầu).
- Scene **AI_Sandbox** chỉ test một subsystem (gather / queue / scout).
- Gắn full `AIBot` + Petra HQ thay cho chỉ `AIController` khi economy ổn định.

---

## 9. Checklist anti-pattern (tóm một dòng)

1. AI logic trùng với `PlayerInput` → duplicate bug.  
2. SO reference lệch → đếm sai → spam build.  
3. Chỉ set một nửa blackboard → gather/return hỏng.  
4. Gán lệnh khi worker build/carry → hủy công.  
5. Target explore không sample NavMesh → scout kẹt map.  
6. Log full verbose → khó đọc, tốn I/O.  
7. `Update` trên mọi unit “cho chắc” → scale kém với số lượng lớn.  
8. Dispatch `Gather` / `ReturnSupplies` mỗi tick cùng node → phá BT loop.

---

## 10. Ánh xạ code hiện tại (UTS)

| Petra / doc | Implementation |
|-------------|----------------|
| `Gather` khi visible | `GatherCommand` + `AIEconomyManager.TryPickGatherTargetForKind` lọc `IsVisible` |
| BT tự return | `AIEconomyManager` bỏ enqueue return; `IsInGatherWorkCycle` → skip worker |
| Sync `SupplySO` | `Worker.SyncGatherBlackboard` + `HandleGatherSupplies` |
| Không gather trùng | `WorkerGatherAssignmentLock`, `ShouldIssueGatherTo` |
| Command path | `AICommandDispatcher` → `GatherCommand.Handle` |
| Không interrupt build | `AIWorkerCommandGuard.CanAssignGatherCommand` |

Cập nhật file này khi thêm manager Petra hoặc đổi hành vi gather/queue.
