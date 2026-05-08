## Tổng quan tài liệu kiến trúc URTS

Tài liệu này là **bản đồ kiến trúc chi tiết** cho thư mục `Assets/Scripts` của URTS, phục vụ cho việc mở rộng thành game RTS kiểu AoE.  
Mỗi phần bên dưới liệt kê:
- **Type** (class/interface/struct/enum), **file**, **base/interfaces**
- **Hàm/properties chính** (public hoặc quan trọng)
- **Chức năng / vai trò**
- **Liên hệ (communication)** với các class/hệ thống khác

> Lưu ý: Một số liên kết phụ thuộc **Inspector** (gán ScriptableObject, tham chiếu prefab, wiring behavior graph) nên không thể hiện đủ 100% trong tài liệu này. Đây là bản đồ chi tiết “từ góc nhìn source code C#”.

---

## 1. Domain `Units`

### 1.1. Core entity & common interfaces

- **`AbstractCommandable`** (`Assets/Scripts/Units/AbstractCommandable.cs`)  
  - **Base/interfaces**: `MonoBehaviour, ISelectable, IDamageable, IHideable`  
  - **Chính**:  
    - `Select()`, `Deselect()`  
    - `TakeDamage(int amount)`  
    - `SetVisible(bool visible)`  
    - `SetCommandOverrides(BaseCommand[] overrides)`  
    - Events: `OnHealthUpdated`, `OnVisibilityChanged`  
  - **Chức năng**:  
    - Base cho mọi entity RTS “có thể chọn và bị phá hủy” (unit, building).  
    - Giữ `Owner`, máu hiện tại/tối đa, trạng thái selection, visibility, danh sách command sẵn có.  
    - Gắn `AbstractUnitSO` clone làm data runtime cho stat.  
  - **Liên hệ**:  
    - **Publish event**: `UnitSelectedEvent`, `UnitDeselectedEvent`, `UnitSpawnEvent`, `UnitDeathEvent`.  
    - **Subscribe**: `UpgradeResearchedEvent` (`Bus<UpgradeResearchedEvent>`) để áp dụng lại upgrade.  
    - Được `PlayerInput`, `RuntimeUI` và UI containers lắng nghe gián tiếp qua event.  

- **`ISelectable`** (`Assets/Scripts/Units/ISelectable.cs`)  
  - **Base/interfaces**: interface  
  - **Chính**: `Select()`, `Deselect()`  
  - **Chức năng**: Hợp đồng cho mọi object có thể được chọn trong game.  
  - **Liên hệ**:  
    - `PlayerInput` raycast vào `ISelectable` khi click/drag.  
    - Implement chính: `AbstractCommandable`.  

- **`IDamageable`** (`Assets/Scripts/Units/IDamageable.cs`)  
  - **Base/interfaces**: interface  
  - **Chính**: `Owner Owner { get; }`, `Transform Transform { get; }`, `void TakeDamage(int amount)`  
  - **Chức năng**: Hợp đồng cho mọi entity có thể bị sát thương.  
  - **Liên hệ**:  
    - Dùng bởi combat/AI (`AttackCommand`, `AttackTargetAction`, `DamageableSensor`).  
    - Implement: `AbstractCommandable` (và do đó mọi unit/building).  

- **`IHideable`** (`Assets/Scripts/Player/IHideable.cs`)  
  - **Base/interfaces**: interface  
  - **Chính**: `bool IsVisible { get; }`, `event Action<IHideable,bool> OnVisibilityChanged`, `void SetVisible(bool)`  
  - **Chức năng**: Chuẩn chung cho mọi thứ bị ảnh hưởng bởi fog of war.  
  - **Liên hệ**:  
    - Implement: `AbstractCommandable`, `GatherableSupply`, `Placeholder`.  
    - `FogVisibilityManager` điều chỉnh visibility và gọi `SetVisible`.  

### 1.2. Units (di chuyển, combat, gather, build, transport)

- **`AbstractUnit`** (`Assets/Scripts/Units/AbstractUnit.cs`)  
  - **Base/interfaces**: `AbstractCommandable, IMoveable, IAttacker`  
  - **Chính**:  
    - `MoveTo(Vector3 position)`, `MoveTo(Transform target)`  
    - `Stop()`  
    - `Attack(IDamageable target)` / `Attack(Vector3 position)`  
  - **Chức năng**:  
    - “Bộ não” unit runtime: wrap `NavMeshAgent` + `BehaviorGraphAgent`.  
    - Thiết lập blackboard cho behavior graph (target, vị trí, lệnh hiện tại).  
  - **Liên hệ**:  
    - Được **command** (`MoveCommand`, `AttackCommand`, `GatherCommand`) gọi qua interface `IMoveable`/`IAttacker`.  
    - **Publish**: `UnitSpawnEvent`, `UnitDeathEvent` (qua base).  
    - **Dùng**: `DamageableSensor` để lấy list target.  

- **`IMoveable`** (`Assets/Scripts/Units/IMoveable.cs`)  
  - Hợp đồng cho entity có thể di chuyển; `MoveTo`, `Stop`.  
  - Dùng bởi `MoveCommand`, hành vi di chuyển trong behavior.  

- **`IAttacker`** (`Assets/Scripts/Units/IAttacker.cs`)  
  - Hợp đồng cho entity có thể tấn công; `Attack(IDamageable/Vector3)`.  
  - Dùng bởi `AttackCommand`, `AttackTargetAction`.  

- **`BaseMilitaryUnit`** (`Assets/Scripts/Units/BaseMilitaryUnit.cs`)  
  - **Base/interfaces**: `AbstractUnit, ITransportable`  
  - **Chức năng**:  
    - Đơn vị quân chuẩn: di chuyển + tấn công + có thể được transport.  
  - **Liên hệ**:  
    - Bị `AirTransport` load/unload (`ITransportable`).  

- **`Grenadier`** (`Assets/Scripts/Units/Grenadier.cs`)  
  - **Base/interfaces**: `BaseMilitaryUnit`  
  - **Chức năng**: unit quân cụ thể (grenadier), dùng `AttackConfigSO` cấu hình damage/aoe.  

- **`Worker`** (`Assets/Scripts/Units/Worker.cs`)  
  - **Base/interfaces**: `AbstractUnit, IBuildingBuilder, ITransportable`  
  - **Chính**:  
    - `Build(BuildingSO building, Vector3 position)`  
    - `ResumeBuilding(BaseBuilding building)`  
    - `CancelBuilding()`  
    - `Gather(IGatherable supply)`  
    - `ReturnSupplies(BaseBuilding commandPost)`  
    - `LoadInto(ITransporter transporter)`  
  - **Chức năng**:  
    - Dân (villager): gather, nộp tài nguyên, xây và sửa nhà, có thể đi nhờ/transport.  
    - Cầu nối giữa command (`GatherCommand`, `BuildBuildingCommand`, `CancelBuildingCommand`) và behavior channel (`BuildingEventChannel`, `GatherSuppliesEventChannel`).  
  - **Liên hệ**:  
    - **Command** gọi methods: `Gather`, `ReturnSupplies`, `Build`, `ResumeBuilding`, `CancelBuilding`.  
    - **Subscribe**:  
      - `GatherSuppliesEventChannel.Event` -> bắn `SupplyEvent` (cộng tài nguyên cho Owner).  
      - `BuildingEventChannel.Event` -> chuyển trạng thái lệnh (build/resume/cancel) qua command override.  

- **`AirTransport`** (`Assets/Scripts/Units/AirTransport.cs`)  
  - **Base/interfaces**: `AbstractUnit, ITransporter`  
  - **Chính**:  
    - `Load(ITransportable unit)`  
    - `Unload(ITransportable unit)`  
    - `UnloadAll()`  
    - `IReadOnlyList<ITransportable> GetLoadedUnits()`  
  - **Chức năng**:  
    - Transport trên không: quản lý danh sách unit đang chở, kiểm tra capacity, xử lý load/unload.  
  - **Liên hệ**:  
    - **Subscribe**: `LoadUnitEventChannel.Event` từ behavior -> thực hiện `Load`.  
    - **Publish**: `UnitLoadEvent`, `UnitUnloadEvent` -> `RuntimeUI` cập nhật panel transport.  
    - **Command**: `UnloadAllUnitsCommand` gọi `UnloadAll()`.  

### 1.3. Buildings

- **`BaseBuilding`** (`Assets/Scripts/Units/BaseBuilding.cs`)  
  - **Base/interfaces**: `AbstractCommandable`  
  - **Chính**:  
    - `IReadOnlyList<UnlockableSO> Queue { get; }`  
    - `BuildingProgress Progress`  
    - `void BuildUnlockable(UnlockableSO unlockable)`  
    - `void CancelBuildingUnit(int index)`  
    - `void StartBuilding(IBuildingBuilder builder)`  
    - Event `OnQueueUpdated`  
  - **Chức năng**:  
    - Đại diện building (TC, Barracks, …):  
      - Khi **chưa hoàn thành**: chứa tiến độ xây dựng (`BuildingProgress`), phối hợp với `Worker`.  
      - Khi **đã xây xong**: quản lý hàng đợi train unit / nghiên cứu upgrade bằng `UnlockableSO`.  
  - **Liên hệ**:  
    - **Publish**: `BuildingSpawnEvent`, `BuildingDeathEvent`, `UpgradeResearchedEvent`, `SupplyEvent` (khi thu/trả tài nguyên từ queue).  
    - **Command**:  
      - `BuildUnitCommand`, `ResearchUpgradeCommand` gọi `BuildUnlockable`.  
      - `BuildBuildingCommand` phối hợp `StartBuilding`.  
    - **UI**: `RuntimeUI` + `BuildingSelectedUI` + `BuildingBuildingUI` + `BuildingUnderConstructionUI` lắng nghe event + đọc `Queue`, `Progress`.  
    - **Tech**: `TechTreeSO` subscribe `BuildingSpawnEvent`/`BuildingDeathEvent`/`UpgradeResearchedEvent`.  

- **`BuildingProgress` / `BuildingState`** (`Assets/Scripts/Units/BuildingProgress.cs`)  
  - **Base/interfaces**: `struct` + `enum`  
  - **Chính**: trạng thái (`BuildingState.Destroyed/Building/Paused/Completed`), thời gian bắt đầu, tiến độ.  
  - **Chức năng**: snapshot giá trị build; logic hoàn thành nằm trong `BaseBuilding`.  

### 1.4. Data SO & enums

- **`AbstractUnitSO`** (`Assets/Scripts/Units/AbstractUnitSO.cs`)  
  - **Base/interfaces**: `UnlockableSO`  
  - **Chính**: `int Health`, `GameObject Prefab`, `UpgradeSO[] Upgrades`, `SightConfigSO SightConfig`  
  - **Chức năng**: data dùng chung cho unit và building.  

- **`UnitSO`** (`Assets/Scripts/Units/UnitSO.cs`)  
  - **Base/interfaces**: `AbstractUnitSO`  
  - **Chức năng**: stat unit (tốc độ, damage, attack config, transport config, v.v.), có `Clone()` để runtime dùng bản riêng.  

- **`BuildingSO`** (`Assets/Scripts/Units/BuildingSO.cs`)  
  - **Base/interfaces**: `AbstractUnitSO`  
  - **Chức năng**: stat building (HP, prefab, cost) + material placement/ghost.  

- **`AttackConfigSO`** (`Assets/Scripts/Units/AttackConfigSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**: thời gian bắn, range, damage, bán kính AOE; `CalculateAreaOfEffectDamage()`.  

- **`TransportConfigSO`** (`Assets/Scripts/Units/TransportConfigSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**: capacity, “mỗi unit chiếm bao nhiêu slot”; `GetTransportCapacityUsage(ITransportable)`.  

- **`SightConfigSO`** (`Assets/Scripts/Units/SightConfigSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**: bán kính tầm nhìn cho fog of war.  

- **`SupplyCostSO`** (`Assets/Scripts/Units/SupplyCostSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**: `Minerals`, `Gas`, chi phí population nếu có.  

- **`UnitCommands`** (`Assets/Scripts/Units/UnitCommands.cs`)  
  - **Base/interfaces**: `enum` (None, Move, Attack, Gather, BuildBuilding, …)  
  - **Chức năng**: trạng thái “lệnh hiện tại” của unit trên blackboard cho behavior.  

- **`Owner`** (`Assets/Scripts/Units/Owner.cs`)  
  - **Base/interfaces**: `enum` (Neutral, Player1, AI1..AI7, v.v.)  
  - **Chức năng**: key để phân vùng event, resources, tech.  

### 1.5. Cảm biến & IK

- **`DamageableSensor`** (`Assets/Scripts/Units/DamageableSensor.cs`)  
  - **Base/interfaces**: `MonoBehaviour` (kèm `[RequireComponent(typeof(SphereCollider))]`)  
  - **Chính**:  
    - `List<IDamageable> Damageables => visibleDamageables.ToList()`  
    - `Owner Owner { get; set; }` (serialize)  
    - Events: `OnUnitEnter`, `OnUnitExit` (delegate `UnitDetectionEvent`)  
    - `SetupFrom(AttackConfigSO attackConfig)` – cấu hình radius theo `AttackRange`.  
    - `OnTriggerEnter(Collider)`, `OnTriggerExit(Collider)`  
    - `HandleVisibilityChange(IHideable,bool)`  
    - `HandleUnitDeath(UnitDeathEvent)`  
  - **Chức năng**:  
    - Vùng cảm biến enemy theo collider hình cầu, chỉ quan tâm **kẻ địch** (`damageable.Owner != Owner`).  
    - Kết hợp với fog-of-war (`IHideable`) để chỉ xử lý các mục tiêu hiện **đang nhìn thấy**.  
    - Quản lý việc subscribe/unsubscribe event `OnVisibilityChanged` và `Bus<UnitDeathEvent>` động theo số target hiện có.  
  - **Liên hệ**:  
    - **Publish event nội bộ**: `OnUnitEnter/OnUnitExit` được unit/behavior dùng để chọn target.  
    - **Subscribe**:  
      - `Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath)` khi có ít nhất 1 target, unregister khi không còn.  
      - `IHideable.OnVisibilityChanged` trên từng kẻ địch trong vùng.  

- **`HoldGunIK`** (`Assets/Scripts/Units/HoldGunIK.cs`)  
  - **Base/interfaces**: `MonoBehaviour`  
  - **Chức năng**: hỗ trợ IK cầm súng; thuần animation, không ảnh hưởng logic gameplay.  

---

## 2. Domain `Commands`

### 2.1. Base & context

- **`BaseCommand`** (`Assets/Scripts/Commands/BaseCommand.cs`)  
  - **Base/interfaces**: `ScriptableObject, ICommand`  
  - **Chính**:  
    - Abstract: `bool CanHandle(CommandContext)`, `void Handle(CommandContext)`, `bool IsLocked(CommandContext)`  
    - Virtual: `bool IsAvailable(CommandContext)`  
    - Helpers: `bool AllRestrictionsPass(CommandContext)`, `bool IsHitColliderVisible(CommandContext)`  
    - Data: `Sprite Icon`, `bool RequiresClickToActivate`, `bool IsSingleUnitCommand`, `GameObject GhostPrefab`, `BuildingRestrictionSO[] Restrictions`, slot index, v.v.  
  - **Chức năng**:  
    - Lớp base cho mọi command (move, attack, gather, build…).  
    - Gom behavior + metadata UI trong một SO, để unit/building chỉ cần trỏ tới asset.  
  - **Liên hệ**:  
    - Được `PlayerInput` gọi qua `Handle(context)` cho từng `AbstractCommandable` được chọn.  
    - Được `ActionsUI` hiển thị bằng `Icon`, `IsAvailable`, `IsLocked`.  

- **`ICommand`** (`Assets/Scripts/Commands/ICommand.cs`)  
  - Interface cơ bản: `Handle` + `CanHandle`.  

- **`CommandContext`** (`Assets/Scripts/Commands/CommandContext.cs`)  
  - **Base/interfaces**: `struct`  
  - **Chính**: `AbstractCommandable Commandable`, `RaycastHit Hit`, `int UnitIndex`, `int Button`, `Owner Owner`  
  - **Chức năng**: gói toàn bộ thông tin cần cho 1 lần xử lý command (ai, click vào đâu, index thứ mấy trong selection, chuột trái/phải).  
  - **Liên hệ**: tạo bởi `PlayerInput` khi dispatch command.  

### 2.2. Concrete commands

> Mỗi command dưới đây đều kế thừa `BaseCommand` và override tối thiểu `Handle`, thường thêm `IsLocked`/`IsAvailable`.

- **`MoveCommand`** (`Assets/Scripts/Commands/MoveCommand.cs`)  
  - Gọi `IMoveable.MoveTo(position)` (tạo đội hình/radial spread).  

- **`AttackCommand`** (`Assets/Scripts/Commands/AttackCommand.cs`)  
  - Nếu `Hit` chứa `IDamageable` địch -> gọi `IAttacker.Attack(target)`; nếu không -> attack point.  

- **`GatherCommand`** (`Assets/Scripts/Commands/GatherCommand.cs`)  
  - Nếu click vào `IGatherable` -> `Worker.Gather`.  
  - Nếu click vào command post thích hợp -> `Worker.ReturnSupplies`.  
  - Ngược lại fallback move.  

- **`BuildBuildingCommand`** (`Assets/Scripts/Commands/BuildBuildingCommand.cs`)  
  - **Base/interfaces**: `BaseCommand, IUnlockableCommand`  
  - **Chính**: `BuildingSO Building`  
  - `IsLocked` / `GetUnmetDependencies()` dùng `TechTreeSO`.  
  - `Handle` kiểm tra resource (`SupplyCostSO`) + restriction (`BuildingRestrictionSO`), spawn placeholder/ghost, gán lệnh build cho `IBuildingBuilder`.  

- **`BuildUnitCommand`** (`Assets/Scripts/Commands/BuildUnitCommand.cs`)  
  - **Base/interfaces**: `BaseCommand, IUnlockableCommand`  
  - Dùng `AbstractUnitSO Unit` để enqueue train trong `BaseBuilding`.  

- **`ResearchUpgradeCommand`** (`Assets/Scripts/Commands/ResearchUpgradeCommand.cs`)  
  - Dùng `UpgradeSO Upgrade`; enqueue vào queue building, kiểm tra `TechTreeSO` + `Supplies`.  

- **`CancelBuildingCommand`** (`Assets/Scripts/Commands/CancelBuildingCommand.cs`)  
  - Gọi `IBuildingBuilder.CancelBuilding()`.  

- **`StopCommand`** (`Assets/Scripts/Commands/StopCommand.cs`)  
  - Gọi `AbstractUnit.Stop()`.  

- **`LoadUnitCommand`** (`Assets/Scripts/Commands/LoadUnitCommand.cs`)  
  - Click vào transporter -> transporter load các `ITransportable` đã chọn.  

- **`LoadIntoCommand`** (`Assets/Scripts/Commands/LoadIntoCommand.cs`)  
  - Click vào transporter khi đang chọn `ITransportable` -> gọi `LoadInto`.  

- **`UnloadAllUnitsCommand`** (`Assets/Scripts/Commands/UnloadAllUnitsCommand.cs`)  
  - Gọi `ITransporter.UnloadAll()`.  

- **`OverrideCommandsCommand`** (`Assets/Scripts/Commands/OverrideCommandsCommand.cs`)  
  - Thay bộ command của entity tạm thời (ví dụ chế độ xây nhà).  

### 2.3. Restrictions

- **`BuildingRestrictionSO`** (`Assets/Scripts/Commands/BuildingRestrictionSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**: `bool CanPlace(RaycastHit hit)`  
  - **Chức năng**: validate vị trí đặt building (overlap collider, navmesh, ground layer…).  
  - **Liên hệ**: dùng bởi `BaseCommand.AllRestrictionsPass` trong `BuildBuildingCommand`.  

- **`OverlapStyle`** (`Assets/Scripts/Commands/BuildingRestrictionSO.cs`)  
  - `enum`: dạng overlap – sphere/box.  

---

## 3. Domain `TechTree`

- **`UnlockableSO`** (`Assets/Scripts/TechTree/UnlockableSO.cs`)  
  - **Base/interfaces**: `ScriptableObject, ICloneable`  
  - **Chính**: `string Name`, `Sprite Icon`, `bool IsOneTimeUnlock`, `float BuildTime`, `SupplyCostSO Cost`, `UnlockableSO[] UnlockRequirements`, `TechTreeSO TechTree`  
  - **Chức năng**: base data cho mọi thứ có thể “mở khóa”: unit, building, upgrade.  

- **`IModifier`** (`Assets/Scripts/TechTree/IModifier.cs`)  
  - Interface: `string PropertyPath`, `void Apply(AbstractUnitSO target)`  

- **`UpgradeSO`** (`Assets/Scripts/TechTree/UpgradeSO.cs`)  
  - **Base/interfaces**: `UnlockableSO, IModifier`  
  - **Chức năng**:  
    - Upgrade trừu tượng, dùng `PropertyPath` để tìm property trên `AbstractUnitSO`, `Apply` áp dụng thay đổi.  
    - Dùng reflection (`GetPropertyValue<T>`) nên linh hoạt nhưng fragile.  

- **`AdditiveIntModifierSO`**, **`AdditiveFloatModifierSO`**  
  - **Base/interfaces**: `UpgradeSO`  
  - **Chính**: override `Apply` để cộng thêm giá trị int/float vào property chỉ định.  

- **`TechTreeSO`** (`Assets/Scripts/TechTree/TechTreeSO.cs`)  
  - **Base/interfaces**: `ScriptableObject`  
  - **Chính**:  
    - `bool IsUnlocked(Owner owner, UnlockableSO unlockable)`  
    - `bool IsResearched(Owner owner, UnlockableSO unlockable)`  
    - `IEnumerable<UnlockableSO> GetUnmetDependencies(Owner owner, UnlockableSO unlockable)`  
  - **Chức năng**:  
    - Lưu trạng thái tech tree theo Owner, quản lý dependency giữa unlockable.  
    - Cập nhật khi building spawn/death hoặc upgrade researched.  
  - **Liên hệ**:  
    - **Subscribe**: `BuildingSpawnEvent`, `BuildingDeathEvent`, `UpgradeResearchedEvent`.  
    - **Dùng bởi**: `BuildBuildingCommand`, `BuildUnitCommand`, `ResearchUpgradeCommand`, UI (`ActionsUI`/tooltips) để biết lock/unlock.  

- **`InvalidPathSpecifiedException`** (`Assets/Scripts/TechTree/InvalidPathSpecifiedException.cs`)  
  - Exception ném khi `PropertyPath` không hợp lệ.  

---

## 4. Domain `Events` & `EventBus`

### 4.1. EventBus

- **`IEvent`** (`Assets/Scripts/EventBus/IEvent.cs`)  
  - Marker interface cho payload của `Bus<T>`.  

- **`Bus<T>`** (`Assets/Scripts/EventBus/Bus.cs`)  
  - **Base/interfaces**: static generic class  
  - **Chính**:  
    - `static event Action<T> OnEvent[Owner owner]` (thực tế là Dictionary<Owner, Event>)  
    - `static void Raise(Owner owner, T evt)`  
    - `static void RegisterForAll(Action<T> handler)` / `UnregisterForAll`  
  - **Chức năng**:  
    - Backbone event pub/sub, tách theo `Owner`.  
  - **Liên hệ**:  
    - Được mọi domain dùng: Units, Buildings, Resources, UI, Tech, Player, Behavior Channels.  

### 4.2. Events

Tất cả ở `Assets/Scripts/Events`, dạng `struct : IEvent` với payload đơn giản:

- Unit: `UnitSpawnEvent`, `UnitDeathEvent`, `UnitSelectedEvent`, `UnitDeselectedEvent`, `UnitLoadEvent`, `UnitUnloadEvent`  
- Building: `BuildingSpawnEvent`, `BuildingDeathEvent`  
- Resource: `SupplyEvent`, `SupplySpawnEvent`, `SupplyDepletedEvent`  
- Tech: `UpgradeResearchedEvent`  
- Input/UI: `CommandSelectedEvent`  
- Fog/placeholder: `PlaceholderSpawnEvent`, `PlaceholderDestroyEvent`  

---

## 5. Domain `Player`

- **`PlayerInput`** (`Assets/Scripts/Player/PlayerInput.cs`)  
  - **Base/interfaces**: `MonoBehaviour`  
  - **Chính**:  
    - Xử lý input camera (pan/zoom/rotate).  
    - Xử lý drag-select bằng `RectTransform selectionBox`.  
    - Click select (raycast vào `ISelectable`).  
    - Biến `ICommand activeCommand`.  
    - `HandleActionSelected(CommandSelectedEvent evt)` – set `activeCommand`.  
    - `HandleRightClick()` – dispatch “lệnh mặc định” (move/attack/gather).  
    - `ActivateAction()` – gọi `activeCommand.Handle(context)` cho từng đơn vị đã chọn.  
  - **Liên hệ**:  
    - **Subscribe**: `CommandSelectedEvent`, `UnitSpawnEvent`, `UnitDeathEvent`, `UnitSelectedEvent`, `UnitDeselectedEvent`.  
    - **Dùng**: `Bus<T>` để sync selection với entity, duy trì `aliveUnits` và `selectedUnits`.  
    - **Gọi**: tất cả `BaseCommand` tương ứng với command đang active.  

- **`Supplies`** (`Assets/Scripts/Player/Supplies.cs`)  
  - **Base/interfaces**: `MonoBehaviour`  
  - **Chính**:  
    - Static `Dictionary<Owner, int> Minerals/Gas/Population/PopulationLimit`  
    - `void HandleSupplyEvent(SupplyEvent evt)`  
    - Cập nhật các `TextMeshProUGUI` resource HUD.  
  - **Chức năng**:  
    - Model tài nguyên + HUD gộp chung; nhận event và cập nhật cả logic lẫn UI.  
  - **Liên hệ**:  
    - **Subscribe**: `SupplyEvent`.  
    - **Producer**: `Worker`, `BaseBuilding`, behavior channels.  

- **`FogVisibilityManager`** (`Assets/Scripts/Player/FogVisibilityManager.cs`)  
  - **Base/interfaces**: `MonoBehaviour`  
  - **Chính**:  
    - Thu thập danh sách `IHideable`, tính toán visibility theo fog render texture.  
    - Trong `LateUpdate` thường xuyên cập nhật `SetVisible` cho hideable.  
  - **Liên hệ**:  
    - **Subscribe**: `UnitSpawnEvent`, `UnitDeathEvent`, `BuildingSpawnEvent`, `BuildingDeathEvent`, `SupplySpawnEvent`, `SupplyDepletedEvent`, `PlaceholderSpawnEvent`, `PlaceholderDestroyEvent`.  
    - Điều khiển hiển thị của `AbstractCommandable`, `GatherableSupply`, `Placeholder`.  

- **`Placeholder`** (`Assets/Scripts/Player/Placeholder.cs`)  
  - **Base/interfaces**: `MonoBehaviour, IHideable`  
  - **Chính**: `SetVisible(bool)`, publish `PlaceholderSpawnEvent`/`PlaceholderDestroyEvent`.  

---

## 6. Domain `UI`

- **`RuntimeUI`** (`Assets/Scripts/UI/RuntimeUI.cs`)  
  - **Base/interfaces**: `MonoBehaviour`  
  - **Chính**:  
    - Giữ `HashSet<AbstractCommandable> selectedUnitsForUI`.  
    - Lắng nghe nhiều event (selection, death, load/unload, building spawn/death, upgrade researched, supply event).  
    - Gọi `RefreshUI()` và điều phối các panel con.  
  - **Liên hệ**: trung tâm UI, kết nối event bus với `ActionsUI`, `BuildingSelectedUI`, `SingleUnitSelectedUI`, `UnitTransportUI`, v.v.  

- **`IUIElement<T...>`** (`Assets/Scripts/UI/IUIElement.cs`)  
  - Interface view: `EnableFor(...)`, `Disable()`.  

- Container chính:  
  - **`ActionsUI`** – hiển thị command grid; raise `CommandSelectedEvent` khi click `UIActionButton`.  
  - **`BuildingSelectedUI`** – bật/tắt `BuildingUnderConstructionUI` / `BuildingBuildingUI` tùy state.  
  - **`BuildingUnderConstructionUI`** – progress xây nhà.  
  - **`BuildingBuildingUI`** – queue train unit/upgrade.  
  - **`SingleUnitSelectedUI`**, **`UnitIconUI`**, **`UnitTransportUI`** – hiển thị thông tin đơn vị/transport.  

- Components:  
  - **`UIActionButton`** – hiển thị command (icon, hotkey, lock reason, cost) và invoke chọn command.  
  - **`UIBuildQueueButton`**, **`UIUnitButton`**, **`Tooltip`**, **`ProgressBar`** – component UI tái sử dụng.  

---

## 7. Domain `Environment`

- **`IGatherable`** (`Assets/Scripts/Environment/IGatherable.cs`)  
  - Interface cho node tài nguyên: `SupplySO Supply`, `int Amount`, `bool IsBusy`, `BeginGather/EndGather/AbortGather`.  

- **`SupplySO`** (`Assets/Scripts/Environment/SupplySO.cs`)  
  - `ScriptableObject` mô tả loại tài nguyên (tối đa, lượng mỗi lần, thời gian gather).  

- **`GatherableSupply`** (`Assets/Scripts/Environment/GatherableSupply.cs`)  
  - **Base/interfaces**: `MonoBehaviour, IGatherable, IHideable`  
  - **Chức năng**: node tài nguyên trên map, raise `SupplySpawnEvent`/`SupplyDepletedEvent`, quản lý visibility theo fog.  

---

## 8. Domain `Behavior`

- **Action (kế thừa `Unity.Behavior.Action`)**  
  - Ví dụ: `AttackTargetAction`, `MoveToTargetLocationAction`, `MoveToTargetGameObjectAction`, `MoveToGatherableSupplyAction`, `GatherSuppliesAction`, `BuildBuildingAction`, …  
  - Override `OnStart/OnUpdate/OnEnd` để đọc/ghi blackboard, gọi vào `AbstractUnit`, `Worker`, `BaseBuilding`, `NavMeshAgent`, v.v.  

- **Condition**  
  - `BuildingIsInProgressCondition`, `GameObjectListSizeCondition` – check trạng thái để quyết định nhánh behavior.  

- **EventChannel (bridge graph <-> code)**  
  - `GatherSuppliesEventChannel`, `BuildingEventChannel`, `LoadUnitEventChannel` – expose event `Event += handler`, và `SendEventMessage(...)` cho graph gọi.  
  - `Worker` và `AirTransport` subscribe các channel này để chuyển message behavior thành logic gameplay (gather, build, load).  

---

## 9. Domain `Utilities`

- **`AnimationConstants`** – chứa hash animator parameter (SPEED, IS_GATHERING, ATTACK).  
- **`ClosestGameObjectComparer`**, **`ClosestColliderComparer`**, **`ClosestCommandPostComparer`** – `IComparer<>` dùng để chọn target gần nhất cho gather/build, v.v.  

---

## 10. Tóm tắt luồng giao tiếp chính

1. **Input → Commands**  
   - `PlayerInput` nhận click/drag, chọn units (`ISelectable`), nhận `CommandSelectedEvent` từ `ActionsUI` và giữ `activeCommand`.  
   - Khi người chơi kích hoạt lệnh, `PlayerInput` tạo `CommandContext` cho từng `AbstractCommandable` đã chọn và gọi `BaseCommand.Handle`.  

2. **Commands → Units/Buildings/Workers**  
   - Các command (Move/Attack/Gather/Build/Research/Transport) gọi vào interface (`IMoveable`, `IAttacker`, `IGatherable`, `IBuildingBuilder`, `ITransporter`) trên `AbstractUnit`, `Worker`, `BaseBuilding`, `AirTransport`.  
   - Các method này thiết lập blackboard/command state để behavior tree thực hiện.  

3. **Behavior Graph → EventChannel → Gameplay**  
   - Behavior graph gọi `SendEventMessage` trên `GatherSuppliesEventChannel`, `BuildingEventChannel`, `LoadUnitEventChannel`.  
   - `Worker`/`AirTransport` subscribe các channel này, và từ đó raise `SupplyEvent`, `Building` event, `UnitLoad/UnitUnload`.  

4. **Gameplay entities → EventBus**  
   - Entities raise `Unit/Building/Supply/Upgrade/Placeholder/CommandSelected` events qua `Bus<T>`.  

5. **EventBus → UI / Tech / Resources / Fog**  
   - `RuntimeUI` lắng nghe event để update panel, command availability.  
   - `Supplies` lắng nghe `SupplyEvent` để cập nhật tài nguyên + HUD.  
   - `TechTreeSO` lắng nghe sự kiện building & upgrade để tính unlock.  
   - `FogVisibilityManager` lắng nghe spawn/death/supply/placeholder để thêm/bớt `IHideable`.  

6. **Fog & Sensors**  
   - `FogVisibilityManager` quyết định `SetVisible` cho mỗi `IHideable`.  
   - `DamageableSensor` trong các unit combat subscribe/unsubscribe `OnVisibilityChanged` của enemy để giữ list target “thật sự nhìn thấy được”.  

---

## 11. Gaps / Hạn chế của bản đồ này

- Wiring chi tiết của **Behavior Graph** (node cụ thể nối với channel nào, sequence ra sao) nằm trong asset `.asset` của Unity, không thấy trực tiếp trong C#.  
- Gán ScriptableObject (command, UnitSO, BuildingSO, UpgradeSO, SupplySO) và config (AvailableCommands array, TechTreeSO reference…) được thực hiện qua **Inspector**, nên tài liệu này chỉ mô tả **khả năng**, không mô tả hết **setup của từng prefab**.  
- Để có bản đồ “full 100%”, cần thêm một bước quét prefab/asset (qua editor tool) để trích ra mapping: Prefab → Component → Trường SerializeField.  

