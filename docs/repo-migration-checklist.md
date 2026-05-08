# Repo Migration Checklist (URTS -> Repo Mới)

Checklist này giúp bạn chuyển ngữ cảnh sang repo mới mà vẫn code đúng chuẩn hiện tại.

## A. Tài liệu cần mang theo

- [ ] Copy toàn bộ thư mục `docs/`
- [ ] Đảm bảo có các file:
  - [ ] `docs/README.md`
  - [ ] `docs/survey-summary.md`
  - [ ] `docs/urts-architecture-map.md`
  - [ ] `docs/repo-migration-checklist.md`
  - [ ] `docs/new-repo-quickstart.md`

## B. Rule/Convention cho AI và team

- [ ] Copy `.cursor/rules/` sang repo mới
- [ ] Kiểm tra rule SOLID + performance cho Unity còn áp dụng đúng
- [ ] Nếu repo mới khác scope, cập nhật rule cho domain mới

## C. Chuẩn cấu trúc source code

- [ ] Tạo/đối chiếu cấu trúc chính dưới `Assets/Scripts`:
  - [ ] `Units`
  - [ ] `Commands`
  - [ ] `TechTree`
  - [ ] `EventBus`
  - [ ] `Events`
  - [ ] `Player`
  - [ ] `UI`
  - [ ] `Environment`
  - [ ] `Behavior`
  - [ ] `Utilities`

## D. Core abstractions nên có ngay

- [ ] `ISelectable`
- [ ] `IDamageable`
- [ ] `IGatherable`
- [ ] `ITransporter` / `ITransportable`
- [ ] `IBuildingBuilder`
- [ ] `ICommand` + `BaseCommand` + `CommandContext`
- [ ] `IEvent` + `Bus<T>`

## E. Data-driven assets tối thiểu

- [ ] `UnitSO` / `BuildingSO` / `AbstractUnitSO`
- [ ] `SupplySO` / `SupplyCostSO`
- [ ] `UnlockableSO` / `UpgradeSO` / `TechTreeSO`
- [ ] `AttackConfigSO` / `SightConfigSO` / `TransportConfigSO` (nếu dùng)

## F. Runtime systems tối thiểu

- [ ] `PlayerInput` hoạt động (selection + command dispatch)
- [ ] `RuntimeUI` hiển thị command cơ bản
- [ ] `Supplies` (hoặc resource model tương đương) nhận `SupplyEvent`
- [ ] `FogVisibilityManager` (nếu dùng fog)
- [ ] Event flow: spawn/death/selection/supply/upgrade chạy thông suốt

## G. Kiểm thử nhanh (sandbox)

- [ ] Sandbox 1: Gather loop (worker -> resource -> command post -> UI)
- [ ] Sandbox 2: Build loop (worker -> place building -> complete)
- [ ] Sandbox 3: Production/research queue
- [ ] Sandbox 4: Combat + visibility (sensor + fog)

## H. Anti-pattern cần tránh ngay

- [ ] Không dùng `GameObject.Find` / `FindObjectOfType` trong runtime logic
- [ ] Không gọi `GetComponent<T>()` trong `Update/FixedUpdate/LateUpdate`
- [ ] Cache component trong `Awake`/`Start`
- [ ] Tránh nhồi nhiều trách nhiệm vào một `MonoBehaviour`

## I. Done criteria trước khi bắt đầu feature mới

- [ ] Team/AI có thể mở `docs/README.md` và hiểu flow trong <10 phút
- [ ] Command -> Event -> UI chạy đúng ít nhất với 1 unit + 1 building + 1 resource
- [ ] Có sandbox scene để test độc lập từng hệ thống
